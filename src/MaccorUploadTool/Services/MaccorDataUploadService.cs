using MaccorUploadTool.Data;
using MaccorUploadTool.Helper;
using MaccorUploadTool.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using NodeService.Infrastructure.NodeSessions;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace MaccorUploadTool.Workers
{
    public class MaccorDataUploadService : BackgroundService
    {
        private readonly MaccorDataReaderWriter _maccorDataReaderWriter;
        private readonly ApiService _apiService;
        private readonly INodeIdentityProvider _nodeIdentityProvider;
        private readonly string _nodeId;
        private Dictionary<string, FileSystemWatcher> _fileSystemWatcherDictionary;
        private ActionBlock<FileSystemChangedRecord> _uploadFileRecordActionBlock;
        private ActionBlock<FileSystemChangedRecord> _kafkaUploadActionBlock;
        private ActionBlock<FileSystemChangedRecord> _fileSystemChangeRecordActionBlock;
        private readonly ConcurrentDictionary<string,object> _files;
        private readonly ConcurrentDictionary<string, FileSystemChangedRecord> _pending;

        private string _ipAddress;

        private readonly ILogger _logger;

        public UploadMaccorDataJobOptions Options { get; private set; }
        public bool IsUpdatingConfig { get; set; }

        public MaccorDataUploadService(
            ILogger<MaccorDataUploadService> logger,
            MaccorDataReaderWriter maccorDataReaderWriter,
            ApiService apiService,
            INodeIdentityProvider nodeIdentityProvider,
            Options options)
        {
            _maccorDataReaderWriter = maccorDataReaderWriter;
            _apiService = apiService;
            _nodeIdentityProvider = nodeIdentityProvider;
            _nodeId = options.NodeId;
            _fileSystemWatcherDictionary = new Dictionary<string, FileSystemWatcher>();
            _files = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _pending = new ConcurrentDictionary<string, FileSystemChangedRecord>(StringComparer.OrdinalIgnoreCase);
            this.Options = new UploadMaccorDataJobOptions
            {
                Directory = options.Directory,
                KafkaConfig = new KafkaConfigModel()
                {
                    BrokerList = options.KafkaBrokerList,
                    Topics =
                    [
                        new StringEntry()
                            {
                                Name="header_topic_name",
                                Value="marcor_header"
                            },
                            new StringEntry()
                            {
                                Name="time_data_topic_name",
                                Value="marcor_time_data"

                            }
                    ]
                }
            };
            this._fileSystemChangeRecordActionBlock = new ActionBlock<FileSystemChangedRecord>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });
            _logger = logger;
            _uploadFileRecordActionBlock = new ActionBlock<FileSystemChangedRecord>(AddOrUpdateFileRecord, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 4
            });
            _kafkaUploadActionBlock = new ActionBlock<FileSystemChangedRecord>(UploadAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 4
            });
        }

        private async Task UploadAsync(FileSystemChangedRecord fileSystemChangedRecord)
        {
            _pending.TryAdd(fileSystemChangedRecord.FullPath, fileSystemChangedRecord);
            this._logger.LogInformation($"Progress {fileSystemChangedRecord.Index}/{_files.Count}");
            this._logger.LogInformation($"Upload {fileSystemChangedRecord.FullPath} to {this.Options.KafkaConfig.BrokerList}");
            Stopwatch stopwatch = Stopwatch.StartNew();
            using DataFileKafkaProducer _kafkaProducer = new DataFileKafkaProducer(
                                _logger,
                fileSystemChangedRecord,
                _maccorDataReaderWriter,
                this.Options.KafkaConfig.BrokerList,
                 this.Options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "header_topic_name").Value,
                 this.Options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "time_data_topic_name").Value
                );
            if (!await _kafkaProducer.ProduceHeaderAsync())
            {
                _kafkaUploadActionBlock.Post(fileSystemChangedRecord);
                return;
            }
            _logger.LogCritical($"Upload {fileSystemChangedRecord.Stat.HeaderDataCount} headers, spent:{stopwatch.Elapsed}");
            fileSystemChangedRecord.Stat.ElapsedMilliSeconds += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            if (!await _kafkaProducer.ProduceTimeDataAsync())
            {
                _kafkaUploadActionBlock.Post(fileSystemChangedRecord);
                return;
            }

            stopwatch.Stop();
            _logger.LogCritical($"Upload {fileSystemChangedRecord.Stat.TimeDataCount} timedata, spent:{stopwatch.Elapsed}");
            fileSystemChangedRecord.Stat.ElapsedMilliSeconds += stopwatch.ElapsedMilliseconds;
            fileSystemChangedRecord.Stat.IsCompleted = true;
            fileSystemChangedRecord.Stat.EndDateTime = DateTime.Now;
            fileSystemChangedRecord.FileRecord.Properties = JsonSerializer.Serialize(fileSystemChangedRecord.Stat);
            _uploadFileRecordActionBlock.Post(fileSystemChangedRecord);
            _logger.LogCritical($"Upload {fileSystemChangedRecord.Stat.FilePath}, spent:{fileSystemChangedRecord.Stat.ElapsedMilliSeconds / 1000d}s");
            _pending.TryRemove(fileSystemChangedRecord.FullPath, out _);
            this._maccorDataReaderWriter.Delete(fileSystemChangedRecord.FullPath);
        }

        private async Task AddOrUpdateFileRecord(FileSystemChangedRecord changeRecord)
        {
            bool repost = true;
            try
            {
                var rsp = await _apiService.AddOrUpdateAsync(changeRecord.FileRecord);
                if (rsp.ErrorCode == 0)
                {
                    repost = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            finally
            {
                if (repost)
                {
                    _uploadFileRecordActionBlock.Post(changeRecord);
                }
                else if(!changeRecord.Stat.IsCompleted)
                {
                    _kafkaUploadActionBlock.Post(changeRecord);
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
            _fileSystemChangeRecordActionBlock = new ActionBlock<FileSystemChangedRecord>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = options.MaxDegreeOfParallelism <= 0 ? 1 : options.MaxDegreeOfParallelism,
                MaxMessagesPerTask = 1
            });
        }

        private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _fileSystemChangeRecordActionBlock.Post(new FileSystemChangedRecord()
            {
                FullPath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        private async Task ConsumeFileSystemChangeRecord(FileSystemChangedRecord fileSystemChangeRecord)
        {
            _logger.LogCritical($"[DataReportService] ConsumeFileSystemChangeRecord {fileSystemChangeRecord.FullPath} at {DateTime.Now}");
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

        private async Task ConsumeFileSystemCreatedRecord(FileSystemChangedRecord fileSystemChangeRecord)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            while (_pending.Count >= 4)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                _logger.LogInformation($"pending {_pending.Count} files");
            }

            DataFileReader dataFileReader = null;
            bool requeue = false;
            if (fileSystemChangeRecord.Stat == null)
            {
                var fileInfo = new FileInfo(fileSystemChangeRecord.FullPath);
                fileSystemChangeRecord.Stat = new MaccorDataUploadStat();
                fileSystemChangeRecord.Stat.DnsName = Dns.GetHostName();
                fileSystemChangeRecord.Stat.NodeId = _nodeId;
                fileSystemChangeRecord.Stat.FilePath = fileSystemChangeRecord.FullPath;
                fileSystemChangeRecord.Stat.BrokerList = Options.KafkaConfig.BrokerList;
                fileSystemChangeRecord.Stat.FileSize = fileInfo.Length;
            }

            try
            {
                fileSystemChangeRecord.Stat.BeginDateTime = DateTime.Now;
                string? fileHashValue = MD5Helper.CalculateFileMD5(fileSystemChangeRecord.FullPath);
                var fileRecord = await GetFileRecordAsync(fileSystemChangeRecord.FullPath);
                if (fileRecord != null && fileRecord.FileHashValue == fileHashValue && IsCompleted(fileRecord))
                {
                    _logger.LogCritical($"file {fileSystemChangeRecord.FullPath} processed, return");
                    return;
                }
                fileSystemChangeRecord.Stat.FileHashValue = fileHashValue;
                if (!DataFileReader.TryLoad(fileSystemChangeRecord.FullPath, _logger, out var ex, out dataFileReader))
                {
                    _logger.LogError($"load {fileSystemChangeRecord.FullPath} fail:{ex}");
                    return;
                }
                ParseDataFile(fileSystemChangeRecord, dataFileReader);
                if (fileRecord == null)
                {
                    fileRecord = new FileRecordModel();
                    fileRecord.Id = _nodeId;
                    fileRecord.Name = fileSystemChangeRecord.FullPath;
                    fileRecord.CreationDateTime = DateTime.Now;
                }
                fileRecord.FileHashValue = fileHashValue;
                fileRecord.ModifyDateTime = DateTime.Now;
                fileRecord.Properties = JsonSerializer.Serialize(fileSystemChangeRecord.Stat);
                fileSystemChangeRecord.FileRecord = fileRecord;
                _uploadFileRecordActionBlock.Post(fileSystemChangeRecord);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(10));
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
                    _fileSystemChangeRecordActionBlock.Post(fileSystemChangeRecord);
                }
            }
        }

        private async void ParseDataFile(FileSystemChangedRecord fileSystemChangeRecord, DataFileReader dataFileReader)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            fileSystemChangeRecord.Stat.BeginDateTime = DateTime.Now;
            _logger.LogInformation($"Processing {fileSystemChangeRecord.FullPath}");
            Stopwatch headerStopWatch = Stopwatch.StartNew();

            fileSystemChangeRecord.DataFile = new DataFile();
            await foreach (var headerArray in dataFileReader.ReadHeadersAsync().ConfigureAwait(false))
            {
                for (int i = 0; i < headerArray.Length; i++)
                {
                    var header = headerArray[i];
                    if (!header.HasValue)
                    {
                        continue;
                    }
                    header.IPAddress = _ipAddress;
                    header.FilePath = fileSystemChangeRecord.FullPath;
                    header.DnsName = fileSystemChangeRecord.Stat.DnsName;
                    headerArray[i] = header;
                    
                    fileSystemChangeRecord.Stat.HeaderDataCount++;
                }

                fileSystemChangeRecord.DataFile.DataFileHeader.Add(headerArray);
            }


            headerStopWatch.Stop();
            fileSystemChangeRecord.Stat.HeaderDataParseElapsedSeconds = headerStopWatch.Elapsed.TotalSeconds;

            Stopwatch timeDataStopWatch = Stopwatch.StartNew();

            _logger.LogInformation($"try delete {fileSystemChangeRecord.FullPath}");
            _maccorDataReaderWriter.Delete(fileSystemChangeRecord.FullPath);

            await foreach (var timeDataArray in dataFileReader.ReadTimeDataAsync().ConfigureAwait(false))
            {
                for (int i = 0; i < timeDataArray.Length; i++)
                {
                    var timeData = timeDataArray[i];
                    if (!timeData.HasValue)
                    {
                        continue;
                    }
                    timeData.IPAddress = _ipAddress;
                    timeData.FilePath = fileSystemChangeRecord.FullPath;
                    timeData.DnsName = fileSystemChangeRecord.Stat.DnsName;
                    timeDataArray[i] = timeData;
                    fileSystemChangeRecord.Stat.TimeDataCount++;
                }
                _maccorDataReaderWriter.WriteTimeDataArray(fileSystemChangeRecord.FullPath, timeDataArray);
                ArrayPool<TimeData>.Shared.Return(timeDataArray, true);
            }
            _logger.LogInformation($"{fileSystemChangeRecord.FullPath}:Write {fileSystemChangeRecord.Stat.TimeDataCount} items");
            timeDataStopWatch.Stop();
            fileSystemChangeRecord.Stat.TimeDataParseElapsedSeconds = timeDataStopWatch.Elapsed.TotalSeconds;

            var scopeTrace = dataFileReader.GetScopeTrace();

            _logger.LogInformation($"Processed {fileSystemChangeRecord.FullPath}");
        }

        private bool IsCompleted(FileRecordModel? fileRecord)
        {
            try
            {
                if (fileRecord.Properties != null)
                {
                    var stat = JsonSerializer.Deserialize<MaccorDataUploadStat>(fileRecord.Properties);
                    if (stat.IsCompleted)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }
            return false;
        }

        private async Task<FileRecordModel?> GetFileRecordAsync(string filePath)
        {
            using var httpClient = new HttpClient();
            FileRecordModel? fileRecord = null;
            try
            {
                var rsp = await _apiService.QueryFileRecordsAsync(_nodeIdentityProvider.GetNodeId(), filePath);
                fileRecord = rsp.Result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(30));
            }

            return fileRecord;
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
            _fileSystemChangeRecordActionBlock.Post(new FileSystemChangedRecord()
            {
                FullPath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        private FileSystemWatcher InitFileSystemWatcher(string path)
        {
            var fileSystemWatcher = new FileSystemWatcher();
            fileSystemWatcher.Path = path;
            fileSystemWatcher.IncludeSubdirectories = true;
            fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
| NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.LastAccess
| NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.Security;
            fileSystemWatcher.Created += _fileSystemWatcher_Created;
            fileSystemWatcher.Error += _fileSystemWatcher_Error;
            fileSystemWatcher.Deleted += _fileSystemWatcher_Deleted;
            fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
            fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
            fileSystemWatcher.EnableRaisingEvents = true;
            return fileSystemWatcher;
        }

        private void ScanDirectory()
        {
            try
            {
                foreach (var directory in Directory.GetDirectories(Options.Directory,
                    "Archive",
                    new EnumerationOptions()
                    {
                        RecurseSubdirectories = true
                    }))
                {
                    if (!this._fileSystemWatcherDictionary.TryGetValue(directory, out var fileSystemWatcher))
                    {
                        fileSystemWatcher = InitFileSystemWatcher(directory);
                        this._fileSystemWatcherDictionary.Add(directory, fileSystemWatcher);
                    }
                    foreach (var filePath in Directory.GetFiles(directory, "*", new EnumerationOptions()
                    {
                        RecurseSubdirectories = true
                    }))
                    {
                        if (!_files.TryAdd(filePath, null))
                        {
                            continue;
                        }
                        this._fileSystemChangeRecordActionBlock.Post(new FileSystemChangedRecord()
                        {
                            ChangeTypes = WatcherChangeTypes.Created,
                            FullPath = filePath,
                            Index = _files.Count
                        });

                    }
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
       

        private void DisposeObjects()
        {
            _fileSystemChangeRecordActionBlock.Complete();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    RefreshIpAddress();
                    ScanDirectory();
                    _logger.LogInformation("Still working");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
   
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }
            DisposeObjects();
        }
    }
}
