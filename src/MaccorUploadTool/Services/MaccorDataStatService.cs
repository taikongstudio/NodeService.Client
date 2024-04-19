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
    public class MaccorDataStatService : BackgroundService
    {
        private readonly ApiService _apiService;
        private readonly string _nodeId;
        private Dictionary<string, FileSystemWatcher> _fileSystemWatcherDictionary;
        private ActionBlock<DataFileContext> _uploadFileRecordActionBlock;
        private ActionBlock<DataFileContext> _fileSystemChangeRecordActionBlock;
        private readonly ConcurrentDictionary<string, object?> _files;

        private string _ipAddress;

        private readonly ILogger _logger;

        private DateTime LimitDateTime;

        public UploadMaccorDataJobOptions Options { get; private set; }
        public bool IsUpdatingConfig { get; set; }

        public MaccorDataStatService(
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
            _files = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _fileSystemChangeRecordActionBlock = new ActionBlock<DataFileContext>(ConsumeFileSystemChangeRecord, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 4,
            });
            _logger = logger;
            _uploadFileRecordActionBlock = new ActionBlock<DataFileContext>(AddOrUpdateFileRecordAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 4,
            });
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

                var fileRecord = await GetFileRecordAsync(fileSystemChangeRecord.LocalFilePath);
                if (fileRecord != null)
                {
                    return;
                }
                if (fileRecord == null)
                {
                    fileRecord = new FileRecordModel();
                    fileRecord.Id = _nodeId;
                    fileRecord.Name = CryptographyHelper.CalculateStringMD5(fileSystemChangeRecord.LocalFilePath);
                    fileRecord.CreationDateTime = File.GetCreationTimeUtc(fileSystemChangeRecord.LocalFilePath);
                    fileRecord.Size = fileSystemChangeRecord.Stat.FileSize;
                }
                fileRecord.ModifyDateTime = DateTime.Now;
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
                if (requeue)
                {
                    _fileSystemChangeRecordActionBlock.Post(fileSystemChangeRecord);
                }
            }
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

        private void ScanDirectory()
        {
            try
            {
                if (!Directory.Exists(Options.Directory))
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
                    ScanDirectory(Options.Directory);
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
    }
}
