using Confluent.Kafka;
using FluentFTP;
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
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace MaccorUploadTool.Services
{
    public class MaccorDataUploadFtpService : BackgroundService
    {
        private readonly ApiService _apiService;
        private readonly string _nodeId;
        private Dictionary<string, FileSystemWatcher> _fileSystemWatcherDictionary;
        private ActionBlock<DataFileContext> _fileSystemChangeRecordActionBlock;
        private readonly ConcurrentDictionary<string, object?> _files;
        private readonly ActionBlock<DataFileContext> _uploadFileRecordActionBlock;
        private ConcurrentQueue<AsyncFtpClient> _ftpClientPool;

        private string _ipAddress;

        private readonly ILogger _logger;

        private DateTime LimitDateTime;
        private List<FileRecordModel> _fileRecords = [];

        public UploadMaccorDataJobOptions UploadOptions { get; private set; }
        public bool IsUpdatingConfig { get; set; }

        public Options Options { get; private set; }

        public MaccorDataUploadFtpService(
            ILogger<MaccorDataUploadKafkaService> logger,
            ApiService apiService,
            Options options)
        {
            _ftpClientPool = new ConcurrentQueue<AsyncFtpClient>();
            this.Options = options;
            LimitDateTime = DateTime.MinValue;
            _apiService = apiService;
            _nodeId = options.NodeId;
            if (!DateTime.TryParse(options.DateTime, out LimitDateTime))
            {
                LimitDateTime = DateTime.MinValue;
            }
            UploadOptions = new UploadMaccorDataJobOptions
            {
                Directory = options.Directory,
            };
            _fileSystemWatcherDictionary = new Dictionary<string, FileSystemWatcher>();
            _files = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _uploadFileRecordActionBlock = new ActionBlock<DataFileContext>(AddOrUpdateFileRecordAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            });
            _fileSystemChangeRecordActionBlock = new ActionBlock<DataFileContext>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : Environment.ProcessorCount
            });
            _logger = logger;
        }

        private async Task AddOrUpdateFileRecordAsync(DataFileContext changeRecord)
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

        private async Task ConsumeFileSystemChangeRecord(DataFileContext fileSystemChangeRecord)
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

        private async Task ConsumeFileSystemCreatedRecord(DataFileContext fileSystemChangeRecord)
        {

            AsyncFtpClient? ftpClient = null;
            try
            {
                var ftpConfig = this.UploadOptions.FtpConfig;
                if (!_ftpClientPool.TryDequeue(out ftpClient))
                {
                    ftpClient = new AsyncFtpClient(ftpConfig.Host, ftpConfig.Username, ftpConfig.Password, ftpConfig.Port);
                }
                await ftpClient.AutoConnect();
                var fileInfo = new FileInfo(fileSystemChangeRecord.LocalFilePath);
                var pathHashValue = CryptographyHelper.CalculateStringMD5(fileSystemChangeRecord.LocalFilePath);
                var fileRecord = _fileRecords.FirstOrDefault(x => x.Name == pathHashValue);
                if (fileRecord != null && fileRecord.State >= FileRecordState.Uploaded && fileRecord.Size == fileInfo.Length)
                {
                    _logger.LogInformation($"Skip upload {fileSystemChangeRecord.LocalFilePath}");
                    return;
                }
                string fileHashValue = CryptographyHelper.CalculateFileMD5(fileSystemChangeRecord.LocalFilePath);
                using var memoryStream = await CompressionHelper.CompressFileAsync(fileSystemChangeRecord.LocalFilePath);
                memoryStream.Position = 0;
                string remoteFilePath = $"/MaccorDataCompressedFiles/{_nodeId}/{pathHashValue}";
                FtpRemoteExists ftpRemoteExists = FtpRemoteExists.Skip;
                if (fileRecord != null && fileRecord.CompressedSize != memoryStream.Length)
                {
                    ftpRemoteExists = FtpRemoteExists.Overwrite;
                }
                var ftpStatus = await ftpClient.UploadStream(memoryStream,
                            remoteFilePath,
                            ftpRemoteExists, true);
                _logger.LogInformation($"{ftpStatus}:Upload {fileSystemChangeRecord.LocalFilePath}");
                memoryStream.Position = 0;
                fileSystemChangeRecord.FileRecord = new FileRecordModel()
                {
                    Id = _nodeId,
                    Name = pathHashValue,
                    OriginalFileName = fileSystemChangeRecord.LocalFilePath,
                    Size = fileInfo.Length,
                    State = FileRecordState.Uploaded,
                    FileHashValue = fileHashValue,
                    CompressedFileHashValue = CryptographyHelper.CalculateStreamMD5(memoryStream),
                    CompressedSize = memoryStream.Length,
                };
                _uploadFileRecordActionBlock.Post(fileSystemChangeRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(10);
                _fileSystemChangeRecordActionBlock.Post(fileSystemChangeRecord);
            }
            finally
            {
                _ftpClientPool.Enqueue(ftpClient);

            }
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

        private async Task<FileRecordModel?> GetFileRecordAsync(string filePath)
        {
            FileRecordModel? fileRecord = null;
            try
            {
                var rsp = await _apiService.QueryFileRecordsAsync(_nodeId, filePath);
                fileRecord = rsp.Result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(10));
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

        private async Task ScanAllDirectoriesAsync()
        {
            try
            {
                var rsp = await this._apiService.QueryFtpConfigAsync(this.Options.FtpConfigId);
                if (rsp.ErrorCode == 0)
                {
                    this.UploadOptions.FtpConfig = rsp.Result;
                }
                if (!Directory.Exists(UploadOptions.Directory))
                {
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        var directory = Path.Combine(drive.RootDirectory.FullName, "Maccor");
                        if (!Directory.Exists(directory))
                        {
                            continue;
                        }
                        ScanDirectory(directory);
                    }
                }
                else
                {
                    ScanDirectory(UploadOptions.Directory);
                }
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
                        Index = _files.Count,
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
                    _fileRecords = await GetFileRecordsAsync();
                    RefreshIpAddress();
                    await ScanAllDirectoriesAsync();
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
