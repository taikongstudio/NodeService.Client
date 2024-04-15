using CommandLine;
using Confluent.Kafka;
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace MaccorUploadTool.Services
{
    public class MaccorDataUploadKafkaService : BackgroundService
    {
        private readonly MaccorDataReaderWriter _maccorDataReaderWriter;
        private readonly ApiService _apiService;
        private readonly string _nodeId;
        private Dictionary<string, FileSystemWatcher> _fileSystemWatcherDictionary;
        private ActionBlock<FileSystemChangedRecord> _uploadFileRecordActionBlock;
        private ActionBlock<FileSystemChangedRecord> _kafkaUploadActionBlock;
        private ActionBlock<FileSystemChangedRecord> _fileSystemChangeRecordActionBlock;
        private readonly ConcurrentDictionary<string, object?> _files;
        private readonly ConcurrentDictionary<string, object?> _parsedUnuploadFiles;

        private long _uploadingFiles;

        private string _ipAddress;

        private readonly ILogger _logger;

        private DateTime LimitDateTime;
        private List<FileRecordModel> _fileRecords = [];

        public UploadMaccorDataJobOptions Options { get; private set; }

        public bool IsUpdatingConfig { get; set; }


        public MaccorDataUploadKafkaService(
            ILogger<MaccorDataUploadKafkaService> logger,
            MaccorDataReaderWriter maccorDataReaderWriter,
            ApiService apiService,
            Options options)
        {
            LimitDateTime = DateTime.MinValue;
            _maccorDataReaderWriter = maccorDataReaderWriter;
            _apiService = apiService;
            _nodeId = options.NodeId;
            if (!DateTime.TryParse(options.DateTime, out LimitDateTime))
            {
                LimitDateTime = DateTime.MinValue;
            }
            _fileSystemWatcherDictionary = new Dictionary<string, FileSystemWatcher>();
            _parsedUnuploadFiles = new ConcurrentDictionary<string, object?>();
            _files = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Options = new UploadMaccorDataJobOptions
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
            _fileSystemChangeRecordActionBlock = new ActionBlock<FileSystemChangedRecord>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });
            _logger = logger;
            _uploadFileRecordActionBlock = new ActionBlock<FileSystemChangedRecord>(AddOrUpdateFileRecordAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 
                Environment.ProcessorCount
            });
            _kafkaUploadActionBlock = new ActionBlock<FileSystemChangedRecord>(UploadAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism =
                Debugger.IsAttached ?
                1 :
                Environment.ProcessorCount
            });
        }

        private async Task UploadAsync(FileSystemChangedRecord fileSystemChangedRecord)
        {
            bool isCompleted = false;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _uploadingFiles);
                _logger.LogInformation($"Start Progress {fileSystemChangedRecord.Index}/{_files.Count}");
                _logger.LogInformation($"Upload {fileSystemChangedRecord.LocalFilePath} to {Options.KafkaConfig.BrokerList}");

                using DataFileKafkaProducer _kafkaProducer = new DataFileKafkaProducer(
                                    _logger,
                    fileSystemChangedRecord,
                    _maccorDataReaderWriter,
                    Options.KafkaConfig.BrokerList,
                     Options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "header_topic_name").Value,
                     Options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "time_data_topic_name").Value
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
                isCompleted = true;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.ToString());
            }
            finally
            {
                fileSystemChangedRecord.Stat.ElapsedMilliSeconds += stopwatch.ElapsedMilliseconds;
                fileSystemChangedRecord.Stat.IsCompleted = isCompleted;
                fileSystemChangedRecord.FileRecord.State = FileRecordState.Processed;
                if (isCompleted)
                {
                    fileSystemChangedRecord.Stat.EndDateTime = DateTime.Now;
                    _logger.LogCritical($"Finish Upload {fileSystemChangedRecord.Stat.FilePath}, spent:{fileSystemChangedRecord.Stat.ElapsedMilliSeconds / 1000d}s");

                }
                fileSystemChangedRecord.FileRecord.Properties = JsonSerializer.Serialize(fileSystemChangedRecord.Stat);
                _uploadFileRecordActionBlock.Post(fileSystemChangedRecord);
                Interlocked.Decrement(ref _uploadingFiles);
                _maccorDataReaderWriter.Delete(fileSystemChangedRecord.LocalFilePath);
                _parsedUnuploadFiles.TryRemove(fileSystemChangedRecord.LocalFilePath, out _);
            }
        }

        private async Task AddOrUpdateFileRecordAsync(FileSystemChangedRecord changeRecord)
        {
            bool repost = true;
            try
            {
                var rsp = await _apiService.AddOrUpdateAsync(changeRecord.FileRecord);
                if (rsp.ErrorCode == 0)
                {
                    _logger.LogInformation($"Add or update {JsonSerializer.Serialize(changeRecord.FileRecord)}");
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
                else if (!changeRecord.Stat.IsCompleted)
                {
                    _kafkaUploadActionBlock.Post(changeRecord);
                }
            }
        }

        private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _fileSystemChangeRecordActionBlock.Post(new FileSystemChangedRecord()
            {
                LocalFilePath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        private async Task ConsumeFileSystemChangeRecord(FileSystemChangedRecord fileSystemChangeRecord)
        {
            _logger.LogCritical($"[DataReportService] ConsumeFileSystemChangeRecord {fileSystemChangeRecord.LocalFilePath} at {DateTime.Now}");
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
            while (_parsedUnuploadFiles.Count >= 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                _logger.LogInformation("Waiting");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            DataFileReader dataFileReader = null;
            bool requeue = false;
            if (fileSystemChangeRecord.Stat == null)
            {
                fileSystemChangeRecord.Stat = new MaccorDataUploadStat();
            }
            var fileInfo = new FileInfo(fileSystemChangeRecord.LocalFilePath);
            fileSystemChangeRecord.Stat.DnsName = Dns.GetHostName();
            fileSystemChangeRecord.Stat.NodeId = _nodeId;
            fileSystemChangeRecord.Stat.FilePath = fileInfo.FullName;
            fileSystemChangeRecord.Stat.BrokerList = Options.KafkaConfig.BrokerList;
            fileSystemChangeRecord.Stat.FileSize = fileInfo.Length;
            fileSystemChangeRecord.Stat.FileCreationDateTimeUtc = fileInfo.CreationTimeUtc;
            fileSystemChangeRecord.Stat.FileModifiedDateTimeUtc = fileInfo.LastWriteTimeUtc;

            try
            {
                fileSystemChangeRecord.Stat.BeginDateTime = DateTime.UtcNow;
                var fileName = fileInfo.FullName;
                var filePathHash = MD5Helper.CalculateStringMD5(fileInfo.FullName);

                var fileRecord = this._fileRecords.FirstOrDefault(x => x.Name == filePathHash);
  
                if (fileRecord != null && fileRecord.State== FileRecordState.Processed)
                {
                    _logger.LogCritical($"file {fileSystemChangeRecord.LocalFilePath} processed, return");
                    return;
                }
                var fileHashValue = MD5Helper.CalculateFileMD5(fileInfo.FullName);
                if (DataFileReader.TryLoad(fileSystemChangeRecord.LocalFilePath, _logger, out var ex, out dataFileReader))
                {
                    if (!await ParseDataFileAsync(fileSystemChangeRecord, dataFileReader))
                    {
                        requeue = true;
                        return;
                    }
                }
                else
                {
                    _logger.LogError($"load {fileSystemChangeRecord.LocalFilePath} fail:{ex}");
                }
                _parsedUnuploadFiles.TryAdd(fileSystemChangeRecord.LocalFilePath, null);
                if (fileRecord == null)
                {
                    fileRecord = new FileRecordModel();
                    fileRecord.Id = _nodeId;
                    fileRecord.Name = filePathHash;
                    fileRecord.OriginalFileName = fileSystemChangeRecord.LocalFilePath;
                    fileRecord.CreationDateTime = File.GetCreationTimeUtc(fileSystemChangeRecord.LocalFilePath);
                    fileRecord.Size = fileSystemChangeRecord.Stat.FileSize;
                    fileRecord.FileHashValue = fileHashValue;
                }
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
                        _logger.LogInformation($"Close {fileSystemChangeRecord.LocalFilePath}");
                    }
                }
                if (requeue)
                {
                    _fileSystemChangeRecordActionBlock.Post(fileSystemChangeRecord);
                    _logger.LogInformation($"Requeue {fileSystemChangeRecord.LocalFilePath}");
                }
            }
        }

        private async Task<bool> ParseDataFileAsync(FileSystemChangedRecord fileSystemChangeRecord, DataFileReader dataFileReader)
        {
            try
            {
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                _logger.LogInformation($"Processing {fileSystemChangeRecord.LocalFilePath}");
                Stopwatch headerStopWatch = Stopwatch.StartNew();

                fileSystemChangeRecord.DataFile = new DataFile();
                await foreach (var dataFileHeader in dataFileReader.ReadHeadersAsync().ConfigureAwait(false))
                {
                    var header = dataFileHeader;
                    header.IPAddress = _ipAddress;
                    header.FilePath = fileSystemChangeRecord.LocalFilePath;
                    header.DnsName = fileSystemChangeRecord.Stat.DnsName;

                    fileSystemChangeRecord.Stat.HeaderDataCount++;
                    fileSystemChangeRecord.DataFile.DataFileHeader.Add(header);
                }


                headerStopWatch.Stop();
                fileSystemChangeRecord.Stat.HeaderDataParseElapsedSeconds = headerStopWatch.Elapsed.TotalSeconds;

                Stopwatch timeDataStopWatch = Stopwatch.StartNew();

                _logger.LogInformation($"try delete {fileSystemChangeRecord.LocalFilePath}");
                _maccorDataReaderWriter.Delete(fileSystemChangeRecord.LocalFilePath);
                int index = 0;

                await foreach (var timeDataArray in dataFileReader.ReadTimeDataAsync().ConfigureAwait(false))
                {
                    int count = 0;
                    for (int i = 0; i < timeDataArray.Length; i++)
                    {
                        var timeData = timeDataArray[i];
                        if (!timeData.HasValue)
                        {
                            continue;
                        }
                        timeData.IPAddress = _ipAddress;
                        timeData.FilePath = fileSystemChangeRecord.LocalFilePath;
                        timeData.DnsName = fileSystemChangeRecord.Stat.DnsName;
                        timeDataArray[i] = timeData;
                        fileSystemChangeRecord.Stat.TimeDataCount++;
                        count++;
                    }
                    _maccorDataReaderWriter.WriteTimeDataArray(fileSystemChangeRecord.LocalFilePath, timeDataArray);
                    _logger.LogInformation($"write {count} items,{index} times");
                    ArrayPool<TimeData>.Shared.Return(timeDataArray, true);
                    index++;
                }
                _logger.LogInformation($"{fileSystemChangeRecord.LocalFilePath}:Write {fileSystemChangeRecord.Stat.TimeDataCount} items");
                timeDataStopWatch.Stop();
                fileSystemChangeRecord.Stat.TimeDataParseElapsedSeconds = timeDataStopWatch.Elapsed.TotalSeconds;

                var scopeTrace = dataFileReader.GetScopeTrace();

                _logger.LogInformation($"Processed {fileSystemChangeRecord.LocalFilePath}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
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
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        private async Task<FileRecordModel?> GetFileRecordAsync(string filePath)
        {
            using var httpClient = new HttpClient();
            FileRecordModel? fileRecord = null;
            try
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName.StartsWith("MaccorFileNameHash_"))
                {
                    fileName = fileName.Replace("MaccorFileNameHash_", string.Empty);
                    filePath = fileName;
                }
                var rsp = await _apiService.QueryFileRecordsAsync(_nodeId, filePath);
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
                LocalFilePath = e.FullPath,
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

        private async void ScanDirectory()
        {
            try
            {
                _fileRecords = await GetFileRecordsAsync();
                ScanDirectory(Options.Directory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private void ScanDirectory(string directory)
        {
            foreach (var archiveDirectory in Directory.GetDirectories(directory,
                "Archive",
                new EnumerationOptions()
                {
                    RecurseSubdirectories = true
                }))
            {
                if (!_fileSystemWatcherDictionary.TryGetValue(archiveDirectory, out var fileSystemWatcher))
                {
                    fileSystemWatcher = InitFileSystemWatcher(archiveDirectory);
                    _fileSystemWatcherDictionary.Add(archiveDirectory, fileSystemWatcher);
                }
                foreach (var filePath in Directory.GetFiles(archiveDirectory, "*", new EnumerationOptions()
                {
                    RecurseSubdirectories = true
                }))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.LastWriteTime < this.LimitDateTime)
                    {
                        continue;
                    }
                    if (!_files.TryAdd(filePath, null))
                    {
                        continue;
                    }
                    _fileSystemChangeRecordActionBlock.Post(new FileSystemChangedRecord()
                    {
                        ChangeTypes = WatcherChangeTypes.Created,
                        LocalFilePath = fileInfo.FullName,
                        Index = _files.Count
                    });

                }
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

        private async Task<List<FileRecordModel>> GetFileRecordsAsync()
        {
            List<FileRecordModel> results = [];
            while (true)
            {
                try
                {
                    List<FileRecordModel> fileRecords = new List<FileRecordModel>();
                    int pageIndex = 0;
                    do
                    {
                        _logger.LogInformation($"{_nodeId}:Query file records ");
                        var rsp = await _apiService.QueryFileRecordsAsync(_nodeId, pageSize: 500, pageIndex: pageIndex);
                        fileRecords.AddRange(rsp.Result);
                        _logger.LogInformation($"{_nodeId}:{fileRecords.Count}/{rsp.TotalCount} ");
                        if (fileRecords.Count == rsp.TotalCount)
                        {
                            break;
                        }
                        pageIndex++;
                    } while (true);

                    results.AddRange(fileRecords);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }


            return results;
        }
    }
}
