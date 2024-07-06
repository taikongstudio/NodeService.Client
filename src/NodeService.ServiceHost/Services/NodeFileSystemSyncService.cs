//using NodeService.Infrastructure.Concurrent;
//using NodeService.Infrastructure.NodeFileSystem;
//using NodeService.ServiceHost.Models;
//using NodeService.ServiceHost.Tasks;
//using System.Collections.Concurrent;

//namespace NodeService.ServiceHost.Services
//{
//    public class NodeFileSystemSyncService : BackgroundService
//    {
//        readonly ILogger<NodeClientService> _logger;
//        readonly IHttpClientFactory _httpClientFactory;
//        readonly NodeFileSystemTable _nodeFileSystemTable;
//        readonly IDisposable? _monitorToken;
//        private readonly IDisposable? _serverOptionsMonitorToken;
//        readonly INodeIdentityProvider _nodeIdentityProvider;
//        ServerOptions _serverOptions;

//        public NodeFileSystemSyncService(
//            ILogger<NodeClientService> logger,
//           INodeIdentityProvider nodeIdentityProvider,
//            IHttpClientFactory httpClientFactory,
//            IOptionsMonitor<ServerOptions> optionsMonitor
//            )
//        {
//            _logger = logger;
//            _httpClientFactory = httpClientFactory;
//            _nodeFileSystemTable = new NodeFileSystemTable();
//            OnServerOptionChanged(optionsMonitor.CurrentValue);
//            _serverOptionsMonitorToken = optionsMonitor.OnChange(OnServerOptionChanged);
//            _nodeIdentityProvider = nodeIdentityProvider;
//        }

//        void OnServerOptionChanged(ServerOptions serverOptions)
//        {
//            _serverOptions = serverOptions;
//        }

//        protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
//        {
//            _logger.LogInformation("Start");
//            await foreach (var array in _fileSystemWatchEventBatchQueue.ReceiveAllAsync(cancellationToken))
//            {

//            }
//        }

//        async ValueTask ProcessNodeFileAddOrUpdateEventAsync(
//            FileInfo fileInfo,
//            FileSystemWatchEventReport  eventReport,
//            CancellationToken cancellationToken = default)
//        {
//            try
//            {
//                var fileSystemWatchConfig = await QueryFileSystemWatchConfigAsync(
//                                        eventReport.ConfigurationId,
//                                        cancellationToken);
//                if (fileSystemWatchConfig == null)
//                {
//                    await DeleteWatcherAsync(eventReport.ConfigurationId, cancellationToken);
//                    return;
//                }

//                if (!_nodeFileSystemTable.TryGetValue(fileInfo.FullName, out var oldRecord) || oldRecord == null)
//                {
//                    oldRecord = new NodeFileSystemWatchRecord()
//                    {
//                        ChangedCount = 0,
//                        LastChangeTime = DateTime.UtcNow,
//                        LastTriggerCount = 0,
//                        LastTriggerTime = DateTime.MinValue,
//                    };
//                }
//               var  newRecord = oldRecord with
//                {
//                    ChangedCount = oldRecord.ChangedCount + 1,
//                    LastChangeTime = DateTime.UtcNow,
//                    LastTriggerCount = oldRecord.LastTriggerCount,
//                    LastTriggerTime = oldRecord.LastTriggerTime,
//                };
//                _nodeFileSystemTable.AddOrUpdate(fileInfo.FullName, newRecord);

//                long changesCount = newRecord.ChangedCount - oldRecord.LastTriggerCount;
//                if (changesCount == 0)
//                {
//                    return;
//                }

//                if (changesCount > fileSystemWatchConfig.TriggerThreshold
//                    &&
//                    DateTime.UtcNow - directoryCounterInfo.LastTriggerTaskTime > TimeSpan.FromSeconds(fileSystemWatchConfig.TimeThreshold))
//                {

//                    switch (fileSystemWatchConfig.EventHandler)
//                    {
//                        case FileSystemWatchEventHandler.Taskflow:
//                            break;
//                        case FileSystemWatchEventHandler.AutoSync:
//                            await AutoSyncAsync(directoryCounterInfo, fileSystemWatchConfig, cancellationToken);
//                            break;
//                        default:
//                            break;
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex.ToString());
//            }

//        }

//        async Task DeleteWatcherAsync(string configId, CancellationToken cancellationToken = default)
//        {
//            var op = new BatchQueueOperation<FileSystemWatchConfigModel, bool>(new FileSystemWatchConfigModel()
//            {
//                Id = configId,
//            }, BatchQueueOperationKind.Delete);
//            await _fileSystemWatchConfigQueue.EnqueueAsync(op, cancellationToken);
//            await op.WaitAsync(cancellationToken);
//        }

//        async ValueTask<FileSystemWatchConfigModel?> QueryFileSystemWatchConfigAsync(
//            string id,
//            CancellationToken cancellationToken = default)
//        {
//            try
//            {
//                using var apiService = CreateApiService();
//                var rsp = await apiService.QueryFileSystemWatchConfigAsync(id, cancellationToken);
//                if (rsp.ErrorCode != 0)
//                {
//                    return null;
//                }
//                return rsp.Result;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex.ToString());
//            }
//            return null;
//        }

//        async ValueTask<FtpUploadConfigModel?> QueryFtpUploadConfigAsync(
//            string id,
//            CancellationToken cancellationToken = default)
//        {
//            try
//            {
//                using var apiService = CreateApiService();
//                var rsp = await apiService.QueryFtpUploadConfigAsync(id, cancellationToken);
//                if (rsp.ErrorCode != 0)
//                {
//                    return null;
//                }
//                return rsp.Result;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex.ToString());
//            }
//            return null;
//        }

//        public override void Dispose()
//        {
//            _monitorToken?.Dispose();
//            _interceptorToken.Dispose();
//            base.Dispose();
//        }

//        async ValueTask AutoSyncAsync(DirectoryChangeInfo directoryCounterInfo, FileSystemWatchConfigModel fileSystemWatchConfig, CancellationToken cancellationToken = default)
//        {
//            if (fileSystemWatchConfig.HandlerContext == null)
//            {
//                return;
//            }

//            var ftpUploadConfig = await QueryFtpUploadConfigAsync(fileSystemWatchConfig.HandlerContext, cancellationToken);
//            if (ftpUploadConfig == null)
//            {
//                return;
//            }
//            var nodeFileSyncDirectoryEnumerator = new NodeFileSystemEnumerator(ftpUploadConfig);

//            var remotePathList = PathHelper.CalculateRemoteFilePath(
//                ftpUploadConfig.LocalDirectory,
//                ftpUploadConfig.RemoteDirectory,
//                filePathList);

//            var fileSystemSyncParameters = new FileSystemSyncParameters
//            {
//                FtpConfigId = ftpUploadConfig.FtpConfigId,
//                NodeId = _nodeIdentityProvider.GetIdentity(),
//                FileSystemWatchConfigurationId = fileSystemWatchConfig.Id,
//                TargetDirectory = PathHelper.CalcuateRemoteDirectory(
//                                            ftpUploadConfig.LocalDirectory,
//                                            directoryCounterInfo.Directory,
//                                            ftpUploadConfig.RemoteDirectory)
//            };

//            var filePathPathList = PathHelper.CalculateRemoteFilePath(
//                ftpUploadConfig.LocalDirectory,
//                ftpUploadConfig.RemoteDirectory,
//                filePathList);

//            foreach (var kv in filePathPathList)
//            {
//                try
//                {
//                    var localFilePath = kv.Key;
//                    var targetFilePath = kv.Value;
//                    var fileInfo = new FileInfo("D:\\Downloads\\Evernote_7.2.2.8065.exe");
//                    string compressedFilePath = null;
//                    FileStream compressedStream = null;
//                    var req = await NodeFileSyncRequestBuilder.FromFileInfoAsync(
//                        "DebugMachine",
//                        "ea61cc81-e1f2-44b0-a90a-a86584da2f9c",
//                        NodeFileSyncConfigurationProtocol.Ftp,
//                        $"/debugtest/{fileInfo.Name}",
//                       fileInfo,
//                        new DefaultSHA256HashAlgorithmProvider(),
//                        new DefaultGzipCompressionProvider(),
//                        (fileInfo) =>
//                        {
//                            compressedFilePath = compressedFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
//                            compressedStream = File.Create(compressedFilePath);
//                            return compressedStream;
//                        });
//                    compressedStream?.Seek(0, SeekOrigin.Begin);
//                    var uploadRsp = await apiService.NodeFileUploadFileAsync(req, compressedStream ?? File.OpenRead(fileInfo.FullName));
//                    File.Delete(compressedFilePath);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex.ToString());
//                }

//            }
//            using ApiService apiService = CreateApiService();
//            var rsp = await apiService.FileSystemSyncAsync(fileSystemSyncParameters);
//            if (rsp.ErrorCode == 0)
//            {
//                if (Debugger.IsAttached)
//                {
//                    foreach (var item in rsp.Result.Progresses)
//                    {
//                        _logger.LogInformation(JsonSerializer.Serialize(item));
//                    }
//                }
//            }
//            else
//            {
//                _logger.LogError(rsp.Message);
//            }
//        }

//        ApiService CreateApiService()
//        {
//            var apiService = new ApiService(_httpClientFactory.CreateClient());
//            apiService.HttpClient.BaseAddress = new Uri(_serverOptions.HttpAddress);
//            apiService.HttpClient.Timeout = TimeSpan.FromMinutes(5);
//            return apiService;
//        }
//    }
//}
