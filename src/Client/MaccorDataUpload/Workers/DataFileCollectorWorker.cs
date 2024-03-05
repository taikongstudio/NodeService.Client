using Google.Protobuf.WellKnownTypes;
using JobsWorker.Shared.DataModels;
using MaccorDataUpload.Data;
using MaccorDataUpload.Helper;
using MaccorDataUpload.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;

namespace MaccorDataUpload.Workers
{
    public class DataFileCollectorWorker : IDisposable
    {
        private FileSystemWatcher _fileSystemWatcher;
        private ActionBlock<FileSystemChangeRecord> _mysqlProcessStatActionBlock;
        private ActionBlock<FileSystemChangeRecord> _updateHashStringActionBlock;
        private ActionBlock<FileSystemChangeRecord> _fileSystemChangeRecordActionBlock;
        private DataFileKafkaProducer _kafkaProducer;
        private string _ipAddress;
        private string currentDbName;

        public string DbName { get; private set; }

        private ILogger _logger;

        public DataReportServiceConfig ServiceConfig { get; private set; }
        public MysqlConfigModel MysqlConfig { get; private set; }
        public bool IsUpdatingConfig { get; set; }

        public DataFileCollectorWorker(ILogger logger)
        {
            this._logger = logger;
            this._mysqlProcessStatActionBlock = new ActionBlock<FileSystemChangeRecord>(ProcessStat);
        }

        private async void ProcessStat(FileSystemChangeRecord fileSystemChangeRecord)
        {
            bool sent = false;
            try
            {
                if (fileSystemChangeRecord == null || fileSystemChangeRecord.UploadFileStat == null)
                {
                    return;
                }
                await SqlHelper.InsertStat(this.MysqlConfig, fileSystemChangeRecord.UploadFileStat, this._logger);
                sent = true;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }
            finally
            {
                if (!sent)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(Random.Shared.Next(1, 5) + Random.Shared.NextDouble()));
                        this._mysqlProcessStatActionBlock.Post(fileSystemChangeRecord);
                    });
                }
            }
        }

        public void UpdateServiceConfig(DataReportServiceConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            this.ServiceConfig = config;
            if (this._fileSystemChangeRecordActionBlock != null)
            {
                this._fileSystemChangeRecordActionBlock.Complete();
            }
            this._fileSystemChangeRecordActionBlock = new ActionBlock<FileSystemChangeRecord>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = config.maxDegreeOfParallelism <= 0 ? 1 : config.maxDegreeOfParallelism,
                MaxMessagesPerTask = 1
            });
            if (this._kafkaProducer != null)
            {
                this._kafkaProducer.Dispose();
            }
            this._kafkaProducer = new DataFileKafkaProducer(this._logger,
                config.broker_list,
                config.header_topic_name,
                config.time_data_topic_name);
        }

        public void UpdateMySqlConfig(MysqlConfigModel mySqlConfig)
        {
            this.MysqlConfig = mySqlConfig;
        }

        public void InitializeSqliteDb(string dbName, bool reimport = false)
        {
            this._logger.LogInformation($"Initial db {dbName},old dbName:{currentDbName}");
            this.DbName = dbName;
            if (this.currentDbName != dbName)
            {
                SqliteDbHelper.Initialize(this.DbName, this._logger);
                this.currentDbName = dbName;
                if (reimport)
                {
                    this.ReimportFiles();
                }

            }
        }

        private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            this._fileSystemChangeRecordActionBlock.Post(new FileSystemChangeRecord()
            {
                FullPath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        private async Task ConsumeFileSystemChangeRecord(FileSystemChangeRecord fileSystemChangeRecord)
        {
            this._logger.LogInformation($"[DataReportService] ConsumeFileSystemChangeRecord {fileSystemChangeRecord.FullPath} at {DateTime.Now}");
            switch (fileSystemChangeRecord.ChangeTypes)
            {
                case WatcherChangeTypes.Created:
                    await this.ConsumeFileSystemCreatedRecord(fileSystemChangeRecord);
                    break;
                case WatcherChangeTypes.Deleted:
                    this._logger.LogInformation("Deleted");
                    break;
                case WatcherChangeTypes.Changed:
                    this._logger.LogInformation("Changed");
                    break;
                case WatcherChangeTypes.Renamed:
                    this._logger.LogInformation("Renamed");
                    break;
                case WatcherChangeTypes.All:
                    this._logger.LogInformation("All");
                    break;
                default:
                    break;
            }
        }

        private async Task ConsumeFileSystemCreatedRecord(FileSystemChangeRecord fileSystemChangeRecord)
        {
            DataFileReader dataFileReader = null;
            bool requeue = false;
            if (fileSystemChangeRecord.UploadFileStat == null)
            {
                fileSystemChangeRecord.UploadFileStat = new UploadFileStat();
                fileSystemChangeRecord.UploadFileStat.host_name = Dns.GetHostName();
                fileSystemChangeRecord.UploadFileStat.file_path = fileSystemChangeRecord.FullPath;
                fileSystemChangeRecord.UploadFileStat.broker_list = this.ServiceConfig.broker_list;
            }
            string? hash = null;
            try
            {
                bool localHasRecord = SqliteDbHelper.ExistsFileRecord(fileSystemChangeRecord.FullPath, this.DbName, this._logger, out hash);
                fileSystemChangeRecord.UploadFileStat.hash_string = hash;
                bool remoteHasRecord = await CheckMysqlDatabaseRecord(fileSystemChangeRecord.UploadFileStat.host_name, fileSystemChangeRecord.FullPath, hash);

                if (localHasRecord && remoteHasRecord)
                {
                    this._logger.LogInformation($"file {fileSystemChangeRecord.FullPath} processed, return");
                    return;
                }
                if (DataFileReader.TryLoad(fileSystemChangeRecord.FullPath, this._logger, out var exception, out dataFileReader))
                {
                    Stopwatch totalStopwatch = Stopwatch.StartNew();
                    fileSystemChangeRecord.UploadFileStat.begin_date_time = DateTime.Now.ToString();
                    this._logger.LogInformation($"Processing {fileSystemChangeRecord.FullPath}");
                    Stopwatch headerStopWatch = Stopwatch.StartNew();
                    foreach (var item in dataFileReader.EnumDataFileHeaders())
                    {
                        item.IPAddress = this._ipAddress;
                        item.FilePath = fileSystemChangeRecord.FullPath;
                        item.DnsName = fileSystemChangeRecord.UploadFileStat.host_name;

                        bool uploadResult = false;
                        for (int i = 0; i < 10; i++)
                        {
                            uploadResult = await _kafkaProducer.ProduceHeaderAsync(null, item.AsJsonString());
                            if (uploadResult)
                            {
                                fileSystemChangeRecord.UploadFileStat.header_data_uploaded_count++;
                                break;
                            }
                            fileSystemChangeRecord.UploadFileStat.header_data_max_retry_times = Math.Max(i, fileSystemChangeRecord.UploadFileStat.header_data_max_retry_times);
                            fileSystemChangeRecord.UploadFileStat.header_data_total_retry_times++;
                            this._logger.LogInformation($"Upload fail:retry {i}");
                            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)));
                        }
                        if (!uploadResult)
                        {
                            requeue = true;
                            return;
                        }
                    }
                    this._kafkaProducer.Flush();

                    headerStopWatch.Stop();
                    fileSystemChangeRecord.UploadFileStat.header_data_elapsed_million_seconds = headerStopWatch.ElapsedMilliseconds;

                    Stopwatch timeDataStopWatch = Stopwatch.StartNew();

                    foreach (var item in dataFileReader.EnumTimeDatas())
                    {
                        item.IPAddress = this._ipAddress;
                        item.FilePath = fileSystemChangeRecord.FullPath;
                        item.DnsName = fileSystemChangeRecord.UploadFileStat.host_name;

                        //Console.WriteLine(item.AsJsonString());
                        bool uploadResult = false;
                        for (int i = 0; i < 10; i++)
                        {
                            uploadResult = await this._kafkaProducer.ProduceTimeDataAsync(null, item.AsJsonString());
                            if (uploadResult)
                            {
                                fileSystemChangeRecord.UploadFileStat.time_data_uploaded_count++;
                                break;
                            }
                            fileSystemChangeRecord.UploadFileStat.time_data_max_retry_times = Math.Max(i, fileSystemChangeRecord.UploadFileStat.time_data_max_retry_times);
                            fileSystemChangeRecord.UploadFileStat.time_data_total_retry_times++;
                            this._logger.LogInformation($"Upload fail:retry {i}");
                            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)));
                        }
                        if (!uploadResult)
                        {
                            requeue = true;
                            return;
                        }

                    }
                    this._kafkaProducer.Flush();
                    timeDataStopWatch.Stop();
                    fileSystemChangeRecord.UploadFileStat.time_data_elapsed_million_seconds = timeDataStopWatch.ElapsedMilliseconds;

                    var scopeTrace = dataFileReader.GetScopeTrace();

                    SqliteDbHelper.InsertFileRecord(fileSystemChangeRecord.FullPath, this.DbName, this._logger);
                    this._logger.LogInformation($"Processed {fileSystemChangeRecord.FullPath}");
                    fileSystemChangeRecord.UploadFileStat.is_completed = true;
                    fileSystemChangeRecord.UploadFileStat.end_date_time = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
                    totalStopwatch.Stop();
                    fileSystemChangeRecord.UploadFileStat.elapsed_milliSeconds = totalStopwatch.ElapsedMilliseconds;
                    this._mysqlProcessStatActionBlock.Post(fileSystemChangeRecord);
                }
                else
                {
                    requeue = true;
                }

            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }
            finally
            {
                if (dataFileReader != null)
                {
                    using (dataFileReader)
                    {
                        this._logger.LogInformation($"Close {fileSystemChangeRecord.FullPath}");
                    }
                }

                if (requeue)
                {
                    this._logger.LogInformation($"Could not process {fileSystemChangeRecord.FullPath}, requeue after 30s");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    fileSystemChangeRecord.UploadFileStat.repost_times++;
                    fileSystemChangeRecord.UploadFileStat.Reset();
                    this._fileSystemChangeRecordActionBlock.Post(fileSystemChangeRecord);
                }
            }
        }

        private async Task<bool> CheckMysqlDatabaseRecord(string host_name, string file_path, string hash)
        {
            int value = 0;
            do
            {
                value = await SqlHelper.UpdateHashString(
                this.MysqlConfig,
                 host_name,
                 file_path,
                 hash,
                 this._logger);
                if (value == -1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }

            } while (value == -1);

            return value > 0;
        }

        private void _fileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {

        }

        private void _fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {

        }

        private void _fileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {

        }

        private void _fileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            this._fileSystemChangeRecordActionBlock.Post(new FileSystemChangeRecord()
            {
                FullPath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        public void Start()
        {
            this._logger.LogInformation("Start", EventLogEntryType.Information);
            try
            {
                if (this._fileSystemWatcher == null)
                {
                    this._fileSystemWatcher = new FileSystemWatcher();
                    this._fileSystemWatcher.Path = ServiceConfig.path;
                    this._fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
        | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.LastAccess
        | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.Security;
                    this._fileSystemWatcher.Created += _fileSystemWatcher_Created;
                    this._fileSystemWatcher.Error += _fileSystemWatcher_Error;
                    this._fileSystemWatcher.Deleted += _fileSystemWatcher_Deleted;
                    this._fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
                    this._fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
                    this._fileSystemWatcher.EnableRaisingEvents = true;
                }
                this.ReimportFiles();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }

        }

        private void ReimportFiles()
        {
            try
            {
                foreach (var item in Directory.GetFiles(this.ServiceConfig.path))
                {
                    this._fileSystemChangeRecordActionBlock.Post(new FileSystemChangeRecord()
                    {
                        ChangeTypes = WatcherChangeTypes.Created,
                        FullPath = item,
                        Name = Path.GetFileName(item),
                    });
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }

        }

        private void RefreshIpAddress()
        {
            string name = Dns.GetHostName();
            IPAddress[] ipadrlist = Dns.GetHostAddresses(name);
            foreach (IPAddress ipa in ipadrlist)
            {
                if (ipa.AddressFamily == AddressFamily.InterNetwork)
                {
                    this._ipAddress = ipa.ToString();
                    break;
                }
            }
        }

        public void Stop()
        {
            try
            {
                this._logger.LogInformation("Stop", EventLogEntryType.Information);
                this._fileSystemWatcher.EnableRaisingEvents = false;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }

        }

        public void Dispose()
        {
            this._fileSystemChangeRecordActionBlock.Complete();
            this._fileSystemWatcher.EnableRaisingEvents = false;
            this._fileSystemWatcher.Created -= _fileSystemWatcher_Created;
            this._fileSystemWatcher.Error -= _fileSystemWatcher_Error;
            this._fileSystemWatcher.Deleted -= _fileSystemWatcher_Deleted;
            this._fileSystemWatcher.Renamed -= _fileSystemWatcher_Renamed;
            this._fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
            this._fileSystemWatcher.Dispose();
            this._kafkaProducer.Dispose();
        }

    }
}
