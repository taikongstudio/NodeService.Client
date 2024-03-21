using Google.Protobuf.WellKnownTypes;
using MaccorUploadTool.Data;
using MaccorUploadTool.Helper;
using MaccorUploadTool.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace MaccorUploadTool.Workers
{
    public class MaccorDataUploadBackgroundService : BackgroundService
    {
        private FileSystemWatcher _fileSystemWatcher;
        private ActionBlock<MaccorDataUploadStat> _postStatActionBlock;
        private ActionBlock<FileSystemChangeRecord> _fileSystemChangeRecordActionBlock;
        private DataFileKafkaProducer _kafkaProducer;
        private string _ipAddress;

        private readonly ILogger Logger;

        public UploadMaccorDataJobOptions Options { get; private set; }
        public bool IsUpdatingConfig { get; set; }

        public MaccorDataUploadBackgroundService(ILogger<MaccorDataUploadBackgroundService> logger)
        {
            Logger = logger;
            _postStatActionBlock = new ActionBlock<MaccorDataUploadStat>(PostStat, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 4
            });
        }

        private async Task PostStat(MaccorDataUploadStat  stat)
        {
            bool sent = false;
            try
            {
                if (stat == null)
                {
                    return;
                }

                using var httpClient = new HttpClient();
                var rsp = await httpClient.PostAsJsonAsync(Options.UploadStatRestApi.RequestUri, stat);
                rsp.EnsureSuccessStatusCode();
                var apiResult = await rsp.Content.ReadFromJsonAsync<ApiResponse<MaccorDataUploadStat>>();
                if (apiResult.ErrorCode == 0)
                {
                    Logger.LogInformation(JsonSerializer.Serialize(apiResult.Result));
                    sent = true;
                }
                else
                {
                    Logger.LogError(apiResult.Message);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            finally
            {
                if (!sent)
                {
                    _postStatActionBlock.Post(stat);
                }
            }
        }

        public void UpdateOptions(UploadMaccorDataJobOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            Options = options;
            if (_fileSystemChangeRecordActionBlock != null)
            {
                _fileSystemChangeRecordActionBlock.Complete();
            }
            _fileSystemChangeRecordActionBlock = new ActionBlock<FileSystemChangeRecord>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = options.maxDegreeOfParallelism <= 0 ? 1 : options.maxDegreeOfParallelism,
                MaxMessagesPerTask = 1
            });
            if (_kafkaProducer != null)
            {
                _kafkaProducer.Dispose();
            }
            _kafkaProducer = new DataFileKafkaProducer(Logger,
                options.KafkaConfig.BrokerList,
                options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "header_topic_name").Value,
                options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "time_data_topic_name").Value
                );
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
            Logger.LogInformation($"[DataReportService] ConsumeFileSystemChangeRecord {fileSystemChangeRecord.FullPath} at {DateTime.Now}");
            switch (fileSystemChangeRecord.ChangeTypes)
            {
                case WatcherChangeTypes.Created:
                    await ConsumeFileSystemCreatedRecord(fileSystemChangeRecord);
                    break;
                case WatcherChangeTypes.Deleted:
                    Logger.LogInformation("Deleted");
                    break;
                case WatcherChangeTypes.Changed:
                    Logger.LogInformation("Changed");
                    break;
                case WatcherChangeTypes.Renamed:
                    Logger.LogInformation("Renamed");
                    break;
                case WatcherChangeTypes.All:
                    Logger.LogInformation("All");
                    break;
                default:
                    break;
            }
        }

        private async Task ConsumeFileSystemCreatedRecord(FileSystemChangeRecord fileSystemChangeRecord)
        {
            DataFileReader dataFileReader = null;
            bool requeue = false;
            if (fileSystemChangeRecord.Stat == null)
            {
                fileSystemChangeRecord.Stat = new MaccorDataUploadStat();
                fileSystemChangeRecord.Stat.DnsName = Dns.GetHostName();
                fileSystemChangeRecord.Stat.FilePath = fileSystemChangeRecord.FullPath;
                fileSystemChangeRecord.Stat.KafkaConfigJsonString = Options.KafkaConfig.ToJsonString<KafkaConfigModel>();
            }

            try
            {
                string? fileHashValue = MD5Helper.CalculateFileMD5(fileSystemChangeRecord.FullPath);
                var stat = await GetStatAsync(fileSystemChangeRecord.Stat.DnsName, fileSystemChangeRecord.FullPath, fileHashValue);

                if (stat != null && stat.FileHashValue == fileHashValue)
                {
                    Logger.LogInformation($"file {fileSystemChangeRecord.FullPath} processed, return");
                    return;
                }
                fileSystemChangeRecord.Stat.FileHashValue = fileHashValue;
                if (DataFileReader.TryLoad(fileSystemChangeRecord.FullPath, Logger, out var exception, out dataFileReader))
                {
                    Stopwatch totalStopwatch = Stopwatch.StartNew();
                    fileSystemChangeRecord.Stat.BeginDateTime = DateTime.Now;
                    Logger.LogInformation($"Processing {fileSystemChangeRecord.FullPath}");
                    Stopwatch headerStopWatch = Stopwatch.StartNew();
                    foreach (var item in dataFileReader.EnumDataFileHeaders())
                    {
                        item.IPAddress = _ipAddress;
                        item.FilePath = fileSystemChangeRecord.FullPath;
                        item.DnsName = fileSystemChangeRecord.Stat.DnsName;

                        bool uploadResult = false;
                        for (int i = 0; i < 10; i++)
                        {
                            uploadResult = await _kafkaProducer.ProduceHeaderAsync(null, item.AsJsonString());
                            if (uploadResult)
                            {
                                fileSystemChangeRecord.Stat.HeaderDataUploadCount++;
                                break;
                            }
                            fileSystemChangeRecord.Stat.HeaderDataMaxRetryTimes = Math.Max(i, fileSystemChangeRecord.Stat.HeaderDataMaxRetryTimes);
                            fileSystemChangeRecord.Stat.HeaderDataTotalRetryTimes++;
                            Logger.LogInformation($"Upload fail:retry {i}");
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
                    fileSystemChangeRecord.Stat.HeaderDataElapsedSeconds = headerStopWatch.Elapsed.TotalSeconds;

                    Stopwatch timeDataStopWatch = Stopwatch.StartNew();

                    foreach (var item in dataFileReader.EnumTimeDatas())
                    {
                        item.IPAddress = _ipAddress;
                        item.FilePath = fileSystemChangeRecord.FullPath;
                        item.DnsName = fileSystemChangeRecord.Stat.DnsName;

                        //Console.WriteLine(item.AsJsonString());
                        bool uploadResult = false;
                        for (int i = 0; i < 10; i++)
                        {
                            uploadResult = await _kafkaProducer.ProduceTimeDataAsync(null, item.AsJsonString());
                            if (uploadResult)
                            {
                                fileSystemChangeRecord.Stat.TimeDataUploadCount++;
                                break;
                            }
                            fileSystemChangeRecord.Stat.TimeDataMaxRetryTimes = Math.Max(i, fileSystemChangeRecord.Stat.TimeDataMaxRetryTimes);
                            fileSystemChangeRecord.Stat.TimeDataTotalRetryTimes++;
                            Logger.LogInformation($"Upload fail:retry {i}");
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
                    fileSystemChangeRecord.Stat.TimeDataElapsedSeconds = timeDataStopWatch.Elapsed.Seconds;

                    var scopeTrace = dataFileReader.GetScopeTrace();

                    Logger.LogInformation($"Processed {fileSystemChangeRecord.FullPath}");
                    fileSystemChangeRecord.Stat.IsCompleted = true;
                    fileSystemChangeRecord.Stat.EndDateTime = DateTime.Now;
                    totalStopwatch.Stop();
                    fileSystemChangeRecord.Stat.ElapsedMilliSeconds = totalStopwatch.ElapsedMilliseconds;
                    _postStatActionBlock.Post(fileSystemChangeRecord.Stat);
                }
                else
                {
                    requeue = true;
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            finally
            {
                if (dataFileReader != null)
                {
                    using (dataFileReader)
                    {
                        Logger.LogInformation($"Close {fileSystemChangeRecord.FullPath}");
                    }
                }

                if (requeue)
                {
                    Logger.LogInformation($"Could not process {fileSystemChangeRecord.FullPath}, requeue after 30s");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    fileSystemChangeRecord.Stat.RepostTimes++;
                    fileSystemChangeRecord.Stat.Reset();
                    _fileSystemChangeRecordActionBlock.Post(fileSystemChangeRecord);
                }
            }
        }

        private async Task<MaccorDataUploadStat?> GetStatAsync(string dnsName, string filePath, string hashValue)
        {
            using var httpClient = new HttpClient();
            MaccorDataUploadStat? stat = null;
            do
            {
                try
                {
                    var rsp = await httpClient.PostAsJsonAsync(Options.QueryStatRestApi.RequestUri,

                        new MaccorDataUploadStatQueryParameters()
                        {
                            DnsName = dnsName,
                            FilePath = filePath,
                            HashValue = hashValue
                        });

                    rsp.EnsureSuccessStatusCode();

                    ApiResponse<MaccorDataUploadStat?> apiResult = await rsp.Content.ReadFromJsonAsync<ApiResponse<MaccorDataUploadStat>>();


                    if (apiResult.ErrorCode == 0)
                    {
                        stat = apiResult.Result;
                        break;
                    }
                    Logger.LogError(apiResult.Message);
                    continue;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }



            } while (true);

            return stat;
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
            Logger.LogInformation("Start");
            try
            {
                if (_fileSystemWatcher == null)
                {
                    InitFileSystemWatcher();
                }
                else if (_fileSystemWatcher.Path != Options.Directory)
                {
                    _fileSystemWatcher.Dispose();
                    _fileSystemWatcher = null;
                    InitFileSystemWatcher();
                }
                ReimportFiles();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

        private void InitFileSystemWatcher()
        {
            _fileSystemWatcher = new FileSystemWatcher();
            _fileSystemWatcher.Path = Options.Directory;
            _fileSystemWatcher.IncludeSubdirectories = true;
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

        private void ReimportFiles()
        {
            try
            {
                foreach (var item in Directory.GetFiles(Options.Directory))
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
                Logger.LogError(ex.ToString());
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
                Logger.LogInformation("Stop");
                _fileSystemWatcher.EnableRaisingEvents = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

       

        private void DisposeObjects()
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
            Start();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    Logger.LogInformation("Still working");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }
            Stop();
            DisposeObjects();
        }
    }
}
