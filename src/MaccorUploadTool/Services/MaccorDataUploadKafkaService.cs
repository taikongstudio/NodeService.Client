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

        private readonly ApiService _apiService;
        private readonly string _nodeId;
        private Dictionary<string, FileSystemWatcher> _fileSystemWatcherDictionary;
        private ActionBlock<DataFileContext> _uploadFileRecordActionBlock;
        private ActionBlock<DataFileContext> _fileSystemChangeRecordActionBlock;
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
            ApiService apiService,
            Options options)
        {
            LimitDateTime = DateTime.MinValue;
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
            _fileSystemChangeRecordActionBlock = new ActionBlock<DataFileContext>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });
            _logger = logger;
            _uploadFileRecordActionBlock = new ActionBlock<DataFileContext>(AddOrUpdateFileRecordAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 
                Environment.ProcessorCount
            });
        }

        private async Task AddOrUpdateFileRecordAsync(DataFileContext context)
        {
            bool repost = true;
            try
            {
                var rsp = await _apiService.AddOrUpdateAsync(context.FileRecord);
                if (rsp.ErrorCode == 0)
                {
                    _logger.LogInformation($"Add or update {JsonSerializer.Serialize(context.FileRecord)}");
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
                    _uploadFileRecordActionBlock.Post(context);
                }
            }
        }

        private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _fileSystemChangeRecordActionBlock.Post(new DataFileContext()
            {
                LocalFilePath = e.FullPath,
                ChangeTypes = e.ChangeType,
                Name = e.Name,
            });
        }

        private async Task ConsumeFileSystemChangeRecord(DataFileContext context)
        {
            _logger.LogCritical($"[DataReportService] ConsumeFileSystemChangeRecord {context.LocalFilePath} at {DateTime.Now}");
            switch (context.ChangeTypes)
            {
                case WatcherChangeTypes.Created:
                    await ConsumeFileSystemCreatedRecord(context);
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

        private async Task ConsumeFileSystemCreatedRecord(DataFileContext context)
        {
            while (_parsedUnuploadFiles.Count >= 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                _logger.LogInformation("Waiting");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            DataFileReader dataFileReader = null;
            bool parseError = false;
            if (context.Stat == null)
            {
                context.Stat = new MaccorDataUploadStat();
            }
            var fileInfo = new FileInfo(context.LocalFilePath);
            context.Stat.DnsName = Dns.GetHostName();
            context.Stat.NodeId = _nodeId;
            context.Stat.FilePath = fileInfo.FullName;
            context.Stat.BrokerList = Options.KafkaConfig.BrokerList;
            context.Stat.FileSize = fileInfo.Length;
            context.Stat.FileCreationDateTimeUtc = fileInfo.CreationTimeUtc;
            context.Stat.FileModifiedDateTimeUtc = fileInfo.LastWriteTimeUtc;

            try
            {
                _parsedUnuploadFiles.TryAdd(context.LocalFilePath, null);
                context.Stat.BeginDateTime = DateTime.UtcNow;
                var fileName = fileInfo.FullName;
                var filePathHash = CryptographyHelper.CalculateStringMD5(fileInfo.FullName);

                var fileRecord = this._fileRecords.FirstOrDefault(x => x.Name == filePathHash);
  
                if (fileRecord != null && fileRecord.State== FileRecordState.Processed)
                {
                    _logger.LogCritical($"file {context.LocalFilePath} processed, return");
                    return;
                }
                var fileHashValue = CryptographyHelper.CalculateFileMD5(fileInfo.FullName);
                if (DataFileReader.TryLoad(context.LocalFilePath, _logger, out var ex, out dataFileReader))
                {
                    context.DataFileReader = dataFileReader;
                    if (!await ProcessDataFileAsync(context))
                    {
                        return;
                    }
                }
                else
                {
                    _logger.LogError($"load {context.LocalFilePath} fail:{ex}");
                }

                if (fileRecord == null)
                {
                    fileRecord = new FileRecordModel();
                    fileRecord.Id = _nodeId;
                    fileRecord.Name = filePathHash;
                    fileRecord.OriginalFileName = context.LocalFilePath;
                    fileRecord.Size = context.Stat.FileSize;
                    fileRecord.FileHashValue = fileHashValue;
                    fileRecord.CompressedFileHashValue = "null";
                    fileRecord.CompressedSize = 0;
                    fileRecord.Category = "Maccor";
                    fileRecord.State = FileRecordState.None;
                }
                if (context.Stat.IsCompleted)
                {
                    fileRecord.State = FileRecordState.Processed;
                }
                fileRecord.Properties = JsonSerializer.Serialize(context.Stat);
                context.FileRecord = fileRecord;
                _uploadFileRecordActionBlock.Post(context);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            finally
            {
                _parsedUnuploadFiles.TryRemove(context.LocalFilePath, out _);
                if (context.DataFileReader != null)
                {
                    using (context.DataFileReader)
                    {
                        _logger.LogInformation($"Close {context.LocalFilePath}");
                    }
                }
            }
        }

        private async Task<bool> ProcessDataFileAsync(DataFileContext context)
        {
            bool uploadCompleted = false;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation($"Processing {context.LocalFilePath}");

                context.DataFile = new DataFile();

                Interlocked.Increment(ref _uploadingFiles);

                _logger.LogInformation($"Start Progress {context.Index}/{_files.Count}");
                _logger.LogInformation($"Upload {context.LocalFilePath} to {Options.KafkaConfig.BrokerList}");

                using DataFileKafkaProducer _kafkaProducer = new DataFileKafkaProducer(
                                    _logger,
                    context,
                    Options.KafkaConfig.BrokerList,
                     Options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "header_topic_name").Value,
                     Options.KafkaConfig.Topics.FirstOrDefault(x => x.Name == "time_data_topic_name").Value
                    );

                var fileName = Path.GetFileName(context.LocalFilePath);

                var parameters = new ReadDataParameters()
                {
                    DnsName = context.Stat.DnsName,
                    IPAddress = _ipAddress,
                    FilePath = fileName,
                };

                foreach (var header in context.DataFileReader.ReadHeaders(parameters))
                {
                    context.Stat.HeaderDataCount++;
                    context.DataFile.DataFileHeader.Add(header);
                }

                if (!await _kafkaProducer.ProduceHeaderAsync())
                {
                    _logger.LogCritical($"Upload {context.Stat.HeaderDataCount} headers, fail");
                    return false;
                }

                context.Stat.HeaderDataParseElapsedSeconds = context.DataFileReader.HeaderDataParseTime.TotalSeconds;

                _logger.LogCritical($"Upload {context.Stat.HeaderDataCount} headers, spent:{stopwatch.Elapsed}");



                uploadCompleted = await UploadTimeDataAsync(context, _kafkaProducer, parameters);

                var scopeTrace = context.DataFileReader.GetScopeTrace();
                if (scopeTrace.Samples.Length > 0)
                {
                    _logger.LogInformation($"scopeTrace:{scopeTrace.Samples.Length}");
                }
                _logger.LogInformation($"Processed {context.LocalFilePath}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                stopwatch.Stop();

                context.Stat.ElapsedMilliSeconds += stopwatch.ElapsedMilliseconds;
                context.Stat.IsCompleted = uploadCompleted;
                if (uploadCompleted)
                {
                    context.Stat.EndDateTime = DateTime.Now;
                    _logger.LogCritical($"Finish Upload {context.Stat.FilePath}, spent:{context.Stat.ElapsedMilliSeconds / 1000d}s");

                }
                Interlocked.Decrement(ref _uploadingFiles);
            }
            return false;
        }

        private async Task<bool> UploadTimeDataAsync(
            DataFileContext context,
            DataFileKafkaProducer _kafkaProducer,
            ReadDataParameters parameters)
        {
            var uploadCompleted = false;

            Stopwatch timeDataStopWatch = Stopwatch.StartNew();

            context.DataFile.TimeDatas = context.DataFileReader.ReadTimeData(parameters)
                .Select(ValidateTimeData);

            if (await _kafkaProducer.ProduceTimeDataAsync())
            {
                return false;
            }

            timeDataStopWatch.Stop();

            context.Stat.TimeDataCount = context.DataFileReader.TimeDataCount;
            _logger.LogCritical($"Upload {context.DataFileReader.TimeDataCount} timedata, spent:{timeDataStopWatch.Elapsed}");


            context.Stat.TimeDataParseElapsedSeconds = context.DataFileReader.TimeDataParseTime.TotalSeconds;
            context.Stat.TimeDataElapsedSeconds = timeDataStopWatch.Elapsed.TotalSeconds;

            return true;
        }

        private static TimeData ValidateTimeData(TimeData timeData,int index)
        {
            if (timeData.Index != index)
            {
                throw new InvalidOperationException();
            }
            return timeData;
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
            _fileSystemChangeRecordActionBlock.Post(new DataFileContext()
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
                    _fileSystemChangeRecordActionBlock.Post(new DataFileContext()
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
            _logger.LogInformation("EB6E7F66-F11C-4F7C-827B-73F387D8F23E");
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
