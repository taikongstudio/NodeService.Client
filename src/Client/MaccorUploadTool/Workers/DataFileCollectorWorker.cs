using MaccorUploadTool.Data;
using MaccorUploadTool.Helper;
using MaccorUploadTool.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;

namespace MaccorUploadTool.Workers
{
    public class DataFileCollectorWorker : BackgroundService
    {
        private MySqlConfig _mySqlConfig;
        private FileSystemWatcher _fileSystemWatcher;
        private ActionBlock<FileSystemChangeRecord> _mysqlActionBlock;
        private ActionBlock<FileSystemChangeRecord> _fileSystemChangeRecordActionBlock;
        private DataFileKafkaProducer _kafkaProducer;
        private string _ipAddress;
        private string currentDbName;

        public string DbName { get; private set; }

        private ILogger<DataFileCollectorWorker> _logger;

        public DataReportServiceConfig ServiceConfig { get; private set; }
        public MySqlConfig MySqlConfig { get; private set; }

        public DataFileCollectorWorker(ILogger<DataFileCollectorWorker> logger,
            MySqlConfig mySqlConfig,
            DataReportServiceConfig dataReportServiceConfig)
        {
            this._logger = logger;
            this._mysqlActionBlock = new ActionBlock<FileSystemChangeRecord>(ProcessStat);
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
                await SqlHelper.InsertStat(MySqlConfig, fileSystemChangeRecord.UploadFileStat, MyLog);
                sent = true;
            }
            catch (Exception ex)
            {
                MyLog(ex.ToString());
            }
            finally
            {
                if (!sent)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(Random.Shared.Next(1, 5) + Random.Shared.NextDouble()));
                        _mysqlActionBlock.Post(fileSystemChangeRecord);
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
            ServiceConfig = config;
            if (_fileSystemChangeRecordActionBlock != null)
            {
                _fileSystemChangeRecordActionBlock.Complete();
            }
            _fileSystemChangeRecordActionBlock = new ActionBlock<FileSystemChangeRecord>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = config.maxDegreeOfParallelism <= 0 ? 1 : config.maxDegreeOfParallelism,
                MaxMessagesPerTask = 1
            });
            if (_kafkaProducer != null)
            {
                _kafkaProducer.Dispose();
            }
            _kafkaProducer = new DataFileKafkaProducer(_logger,
                config.broker_list,
                config.header_topic_name,
                config.time_data_topic_name);
        }

        public void UpdateMySqlConfig(MySqlConfig mySqlConfig)
        {
            MySqlConfig = mySqlConfig;
        }

        public void InitializeDb(string dbName)
        {
            _logger.LogInformation($"Initial db {dbName},old dbName:{currentDbName}");
            DbName = dbName;
            if (currentDbName != dbName)
            {
                SqliteDbHelper.Initialize(DbName, _logger);
                currentDbName = dbName;
                ReimportFiles();
            }
        }

        private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _fileSystemChangeRecordActionBlock.Post(new FileSystemChangeRecord()
            {
                FullPath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        private async Task ConsumeFileSystemChangeRecord(FileSystemChangeRecord fileSystemChangeRecord)
        {
            _logger.LogInformation($"[DataReportService] ConsumeFileSystemChangeRecord {fileSystemChangeRecord.FullPath} at {DateTime.Now}");
            switch (fileSystemChangeRecord.ChangeTypes)
            {
                case WatcherChangeTypes.Created:
                    await ConsumeFileSystemCreatedRecord(fileSystemChangeRecord);
                    break;
                case WatcherChangeTypes.Deleted:
                    _logger.LogInformation("Deleted");
                    break;
                case WatcherChangeTypes.Changed:
                    _logger.LogInformation("Changed");
                    break;
                case WatcherChangeTypes.Renamed:
                    _logger.LogInformation("Renamed");
                    break;
                case WatcherChangeTypes.All:
                    _logger.LogInformation("All");
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
                fileSystemChangeRecord.UploadFileStat.begin_date_time = DateTime.Now.ToString();
                fileSystemChangeRecord.UploadFileStat.broker_list = ServiceConfig.broker_list;
            }
            try
            {
                RefreshIpAddress();
                if (SqliteDbHelper.ExistsFileRecord(fileSystemChangeRecord.FullPath, DbName, _logger))
                {
                    _logger.LogInformation($"file {fileSystemChangeRecord.FullPath} processed, return", EventLogEntryType.Information);
                    return;
                }
                if (DataFileReader.TryLoad(fileSystemChangeRecord.FullPath, _logger, out var exception, out dataFileReader))
                {
                    Stopwatch totalStopwatch = Stopwatch.StartNew();
                    fileSystemChangeRecord.UploadFileStat.file_path = fileSystemChangeRecord.FullPath;
                    fileSystemChangeRecord.UploadFileStat.host_name = Dns.GetHostName();
                    fileSystemChangeRecord.UploadFileStat.begin_date_time = DateTime.Now.ToString();
                    var dnsName = Dns.GetHostName();
                    _logger.LogInformation($"Processing {fileSystemChangeRecord.FullPath}", EventLogEntryType.Information);
                    Stopwatch headerStopWatch = Stopwatch.StartNew();
                    foreach (var item in dataFileReader.EnumDataFileHeaders())
                    {
                        item.IPAddress = _ipAddress;
                        item.FilePath = fileSystemChangeRecord.FullPath;
                        item.DnsName = dnsName;

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
                            _logger.LogInformation($"Upload fail:retry {i}");
                            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)));
                        }
                        if (!uploadResult)
                        {
                            requeue = true;
                            return;
                        }
                    }
                    _kafkaProducer.Flush();

                    headerStopWatch.Stop();
                    fileSystemChangeRecord.UploadFileStat.header_data_elapsed_million_seconds = headerStopWatch.ElapsedMilliseconds;

                    Stopwatch timeDataStopWatch = Stopwatch.StartNew();

                    foreach (var item in dataFileReader.EnumTimeDatas())
                    {
                        item.IPAddress = _ipAddress;
                        item.FilePath = fileSystemChangeRecord.FullPath;
                        item.DnsName = dnsName;

                        //Console.WriteLine(item.AsJsonString());
                        bool uploadResult = false;
                        for (int i = 0; i < 10; i++)
                        {
                            uploadResult = await _kafkaProducer.ProduceTimeDataAsync(null, item.AsJsonString());
                            if (uploadResult)
                            {
                                fileSystemChangeRecord.UploadFileStat.time_data_uploaded_count++;
                                break;
                            }
                            fileSystemChangeRecord.UploadFileStat.time_data_max_retry_times = Math.Max(i, fileSystemChangeRecord.UploadFileStat.time_data_max_retry_times);
                            fileSystemChangeRecord.UploadFileStat.time_data_total_retry_times++;
                            _logger.LogInformation($"Upload fail:retry {i}");
                            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)));
                        }
                        if (!uploadResult)
                        {
                            requeue = true;
                            return;
                        }

                    }
                    _kafkaProducer.Flush();
                    timeDataStopWatch.Stop();
                    fileSystemChangeRecord.UploadFileStat.time_data_elapsed_million_seconds = timeDataStopWatch.ElapsedMilliseconds;

                    var scopeTrace = dataFileReader.GetScopeTrace();

                    SqliteDbHelper.InsertFileRecord(fileSystemChangeRecord.FullPath, DbName, _logger);
                    _logger.LogInformation($"Processed {fileSystemChangeRecord.FullPath}", EventLogEntryType.Information);
                    fileSystemChangeRecord.UploadFileStat.is_completed = true;
                    fileSystemChangeRecord.UploadFileStat.end_date_time = DateTime.Now.ToString();
                    totalStopwatch.Stop();
                    fileSystemChangeRecord.UploadFileStat.elapsed_milliSeconds = totalStopwatch.ElapsedMilliseconds;
                    _mysqlActionBlock.Post(fileSystemChangeRecord);
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
                        _logger.LogInformation($"Close {fileSystemChangeRecord.FullPath}");
                    }
                }

                if (requeue)
                {
                    _logger.LogInformation($"Could not process {fileSystemChangeRecord.FullPath}, requeue after 5 min", EventLogEntryType.Information);
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    fileSystemChangeRecord.UploadFileStat.repost_times++;
                    fileSystemChangeRecord.UploadFileStat.Reset();
                    _fileSystemChangeRecordActionBlock.Post(fileSystemChangeRecord);
                }


            }
        }

        private void MyLog(string message)
        {
            this._logger.LogInformation(message);
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
            _fileSystemChangeRecordActionBlock.Post(new FileSystemChangeRecord()
            {
                FullPath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        public void Start()
        {
            _logger.LogInformation("Start", EventLogEntryType.Information);
            try
            {
                if (_fileSystemWatcher == null)
                {
                    _fileSystemWatcher = new FileSystemWatcher();
                    _fileSystemWatcher.Path = ServiceConfig.path;
                    _fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
        | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.LastAccess
        | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.Security;
                    _fileSystemWatcher.Created += _fileSystemWatcher_Created;
                    _fileSystemWatcher.Error += _fileSystemWatcher_Error;
                    _fileSystemWatcher.Deleted += _fileSystemWatcher_Deleted;
                    _fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
                    _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
                    _fileSystemWatcher.EnableRaisingEvents = true;
                }
                ReimportFiles();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private void ReimportFiles()
        {
            try
            {
                foreach (var item in Directory.GetFiles(ServiceConfig.path))
                {
                    _fileSystemChangeRecordActionBlock.Post(new FileSystemChangeRecord()
                    {
                        ChangeTypes = WatcherChangeTypes.Created,
                        FullPath = item,
                        Name = Path.GetFileName(item),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
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
                    _ipAddress = ipa.ToString();
                    break;
                }
            }
        }

        public void Stop()
        {
            try
            {
                _logger.LogInformation("Stop", EventLogEntryType.Information);
                Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        public void Dispose()
        {
            _fileSystemChangeRecordActionBlock.Complete();
            _fileSystemWatcher.EnableRaisingEvents = false;
            _fileSystemWatcher.Created -= _fileSystemWatcher_Created;
            _fileSystemWatcher.Error -= _fileSystemWatcher_Error;
            _fileSystemWatcher.Deleted -= _fileSystemWatcher_Deleted;
            _fileSystemWatcher.Renamed -= _fileSystemWatcher_Renamed;
            _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
            _fileSystemWatcher.Dispose();

            _kafkaProducer.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                await Task.Delay(10000);
            }
        }
    }
}
