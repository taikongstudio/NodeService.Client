using NodeService.Infrastructure.Concurrent;
using NodeService.ServiceHost.Helpers;
using NodeService.ServiceHost.Models;
using NodeService.ServiceHost.Tasks;
using System.Collections.Concurrent;
using System.Security.Authentication;

namespace NodeService.ServiceHost.Services
{
    public class NodeFileSystemSyncService : BackgroundService
    {
        class NodeFileSystemSyncContext
        {
            public NodeFileSystemSyncContext(NodeConfigurationDirectoryKey key, IEnumerable<FileSystemWatchEventReport> reports)
            {
                Key = key;
                Reports = reports;
            }

            public NodeConfigurationDirectoryKey Key { get; private set; }

            public FileSystemWatchConfigModel FileSystemWatchConfig { get; set; }
            public IEnumerable<FileSystemWatchEventReport> Reports { get; private set; }

            public static NodeFileSystemSyncContext From(IGrouping<NodeConfigurationDirectoryKey, FileSystemWatchEventReport> groups)
            {
                return new NodeFileSystemSyncContext(groups.Key, groups);
            }
        }


        readonly ILogger<NodeClientService> _logger;
        private readonly IAsyncQueue<BatchQueueOperation<FileSystemWatchConfigModel, bool>> _fileSystemWatchConfigQueue;
        readonly IDisposable _token;
        readonly BatchQueue<FileSystemWatchEventReport> _fileSystemWatchEventBatchQueue;
        readonly ConcurrentDictionary<NodeConfigurationDirectoryKey, DirectoryCounterInfo> _directoryCounterDict;
        readonly IDisposable? _monitorToken;
        readonly ApiService _apiService;
        readonly INodeIdentityProvider _nodeIdentityProvider;
        ServerOptions _serverOptions;

        public NodeFileSystemSyncService(
            ILogger<NodeClientService> logger,
            [FromKeyedServices(nameof(NodeClientService))] IAsyncQueue<FileSystemWatchEventReport> sourceQueue,
            [FromKeyedServices(nameof(NodeFileSystemWatchService))] IAsyncQueue<BatchQueueOperation<FileSystemWatchConfigModel, bool>> fileSystemWatchConfigQueue,
            INodeIdentityProvider nodeIdentityProvider,
            HttpClient httpClient,
            IOptionsMonitor<ServerOptions> optionsMonitor
            )
        {
            _logger = logger;
            _fileSystemWatchConfigQueue = fileSystemWatchConfigQueue;
            _token = sourceQueue.RegisterInterceptor(SendBatchQueueAsync);
            _fileSystemWatchEventBatchQueue = new BatchQueue<FileSystemWatchEventReport>(1024, TimeSpan.FromSeconds(10));
            _directoryCounterDict = new();
            _apiService = new ApiService(httpClient);
            OnServerOptionChanged(optionsMonitor.CurrentValue);
            _nodeIdentityProvider = nodeIdentityProvider;
        }

        void OnServerOptionChanged(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
            _apiService.HttpClient.BaseAddress = new Uri(_serverOptions.HttpAddress);
            _apiService.HttpClient.Timeout = TimeSpan.FromSeconds(200);
        }

        async ValueTask SendBatchQueueAsync(FileSystemWatchEventReport fileSystemWatchEventReport)
        {
            if (fileSystemWatchEventReport.Created != null
                ||
                fileSystemWatchEventReport.Changed != null
                ||
                fileSystemWatchEventReport.Deleted != null
                ||
                fileSystemWatchEventReport.Renamed != null
                )
            {
                await _fileSystemWatchEventBatchQueue.SendAsync(fileSystemWatchEventReport);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Start");
            await foreach (var arrayPoolCollection in _fileSystemWatchEventBatchQueue.ReceiveAllAsync(cancellationToken))
            {
                try
                {
                    var directoryFileSyncContexts = arrayPoolCollection.GroupBy(NodeConfigurationDirectoryKey.Create).Select(NodeFileSystemSyncContext.From);

                    if (Debugger.IsAttached)
                    {
                        foreach (var context in directoryFileSyncContexts)
                        {
                            await ProcessNodeFileSystemSyncContextdAsync(context, cancellationToken);
                        }
                    }
                    else
                    {
                        await Parallel.ForEachAsync(directoryFileSyncContexts, new ParallelOptions()
                        {
                            CancellationToken = cancellationToken,
                            MaxDegreeOfParallelism = 4,
                        }, ProcessNodeFileSystemSyncContextdAsync);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }


            }
        }

        async ValueTask ProcessNodeFileSystemSyncContextdAsync(
            NodeFileSystemSyncContext nodeFileSystemSyncContext,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (nodeFileSystemSyncContext.Key.Directory == null)
                {
                    return;
                }
                var fileSystemWatchConfig = await QueryFileSystemWatchConfigAsync(
                                        nodeFileSystemSyncContext.Key.ConfigurationId,
                                        cancellationToken);
                if (fileSystemWatchConfig == null)
                {
                    await DeleteWatcherAsync(nodeFileSystemSyncContext.Key.ConfigurationId, cancellationToken);
                    return;
                }


                if (!_directoryCounterDict.TryGetValue(nodeFileSystemSyncContext.Key, out var directoryCounterInfo))
                {
                    directoryCounterInfo = new DirectoryCounterInfo(nodeFileSystemSyncContext.Key.Directory);
                    _directoryCounterDict.TryAdd(nodeFileSystemSyncContext.Key, directoryCounterInfo);
                }

                foreach (var eventReport in nodeFileSystemSyncContext.Reports)
                {
                    switch (eventReport.EventCase)
                    {
                        case FileSystemWatchEventReport.EventOneofCase.None:
                            break;
                        case FileSystemWatchEventReport.EventOneofCase.Created:
                            directoryCounterInfo.CreatedCount++;
                            directoryCounterInfo.TotalCount++;
                            directoryCounterInfo.PathList.Add(eventReport.Created.FullPath);
                            break;
                        case FileSystemWatchEventReport.EventOneofCase.Changed:
                            directoryCounterInfo.ChangedCount++;
                            directoryCounterInfo.TotalCount++;
                            directoryCounterInfo.PathList.Add(eventReport.Changed.FullPath);
                            break;
                        case FileSystemWatchEventReport.EventOneofCase.Deleted:
                            directoryCounterInfo.DeletedCount++;
                            directoryCounterInfo.TotalCount++;
                            directoryCounterInfo.PathList.Add(eventReport.Deleted.FullPath);
                            break;
                        case FileSystemWatchEventReport.EventOneofCase.Renamed:
                            directoryCounterInfo.RenamedCount++;
                            directoryCounterInfo.TotalCount++;
                            directoryCounterInfo.PathList.Add(eventReport.Renamed.FullPath);
                            break;
                        case FileSystemWatchEventReport.EventOneofCase.Error:
                            break;
                        default:
                            break;
                    }
                }
                long changesCount = directoryCounterInfo.TotalCount - directoryCounterInfo.LastTriggerCount;
                if (changesCount == 0)
                {
                    return;
                }


                if (changesCount > fileSystemWatchConfig.TriggerThreshold
                    &&
                    DateTime.UtcNow - directoryCounterInfo.LastTriggerTaskTime > TimeSpan.FromSeconds(fileSystemWatchConfig.TimeThreshold))
                {

                    switch (fileSystemWatchConfig.EventHandler)
                    {
                        case FileSystemWatchEventHandler.Taskflow:
                            break;
                        case FileSystemWatchEventHandler.AutoSync:
                            await AutoSyncAsync(directoryCounterInfo, fileSystemWatchConfig, cancellationToken);
                            break;
                        default:
                            break;
                    }
                    directoryCounterInfo.PathList.Clear();
                    directoryCounterInfo.LastTriggerTaskTime = DateTime.UtcNow;
                    directoryCounterInfo.LastTriggerCount = directoryCounterInfo.TotalCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        async Task DeleteWatcherAsync(string configId, CancellationToken cancellationToken = default)
        {
            var op = new BatchQueueOperation<FileSystemWatchConfigModel, bool>(new FileSystemWatchConfigModel()
            {
                Id = configId,
            }, BatchQueueOperationKind.Delete);
            await op.WaitAsync(cancellationToken);
        }

        async ValueTask<FileSystemWatchConfigModel?> QueryFileSystemWatchConfigAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var rsp = await _apiService.QueryFileSystemWatchConfigAsync(id, cancellationToken);
                if (rsp.ErrorCode != 0)
                {
                    return null;
                }
                return rsp.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return null;
        }

        async ValueTask<FtpUploadConfigModel?> QueryFtpUploadConfigAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var rsp = await _apiService.QueryFtpUploadConfigAsync(id, cancellationToken);
                if (rsp.ErrorCode != 0)
                {
                    return null;
                }
                return rsp.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return null;
        }

        public override void Dispose()
        {
            _monitorToken?.Dispose();
            _token.Dispose();
            base.Dispose();
        }

        async ValueTask AutoSyncAsync(DirectoryCounterInfo directoryCounterInfo, FileSystemWatchConfigModel fileSystemWatchConfig, CancellationToken cancellationToken = default)
        {
            if (fileSystemWatchConfig.HandlerContext == null)
            {
                return;
            }

            var ftpUploadConfig = await QueryFtpUploadConfigAsync(fileSystemWatchConfig.HandlerContext, cancellationToken);
            if (ftpUploadConfig == null)
            {
                return;
            }
            var nodeFileSyncDirectoryEnumerator = new NodeFileSystemEnumerator(ftpUploadConfig);
            var filePathList = nodeFileSyncDirectoryEnumerator.EnumerateFiles(directoryCounterInfo.Directory);
            if (directoryCounterInfo.PathList != null)
            {
                filePathList = directoryCounterInfo.PathList.Intersect(filePathList);
            }
            if (!filePathList.Any())
            {
                return;
            }

            var remotePathList = PathHelper.CalculateRemoteFilePath(
                ftpUploadConfig.LocalDirectory,
                ftpUploadConfig.RemoteDirectory,
                filePathList);

            var fileSystemSyncParameters = new FileSystemSyncParameters
            {
                FtpConfigId = ftpUploadConfig.FtpConfigId,
                NodeId = _nodeIdentityProvider.GetIdentity(),
                FileSystemWatchConfigurationId = fileSystemWatchConfig.Id,
                TargetDirectory = PathHelper.CalcuateRemoteDirectory(
                                            ftpUploadConfig.LocalDirectory,
                                            directoryCounterInfo.Directory,
                                            ftpUploadConfig.RemoteDirectory)
            };

            foreach (var kv in remotePathList)
            {
                try
                {
                    var localFilePath = kv.Key;
                    var targetFilePath = kv.Value;
                    var fileSystemSyncInfo = await FileSystemFileSyncInfo.FromFileInfoAsync(
                        new FileInfo(localFilePath),
                        targetFilePath,
                        fileSystemWatchConfig.CompressThreshold);
                    fileSystemSyncParameters.FileSyncInfoList.Add(fileSystemSyncInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }
            var rsp = await _apiService.FileSystemSyncAsync(fileSystemSyncParameters);
            if (rsp.ErrorCode == 0)
            {
                if (Debugger.IsAttached)
                {
                    foreach (var item in rsp.Result.Progresses)
                    {
                        _logger.LogInformation(JsonSerializer.Serialize(item));
                    }
                }
            }
            else
            {
                _logger.LogError(rsp.Message);
            }
        }


    }
}
