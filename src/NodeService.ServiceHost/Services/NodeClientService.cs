using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client.Web;
using NodeService.Infrastructure.Concurrent;
using NodeService.ServiceHost.Models;
using System.Threading;
using static NodeService.Infrastructure.Services.NodeService;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService : BackgroundService
    {


        private class SubscribeEventInfo
        {
            public required NodeServiceClient Client { get; set; }

            public required SubscribeEvent SubscribeEvent { get; set; }

            public required CancellationToken CancellationToken { get; set; }
        }


        readonly IAsyncQueue<BatchQueueOperation<FileSystemWatchConfigModel, bool>> _fileSystemConfigurationQueue;
        readonly ActionBlock<SubscribeEventInfo> _subscribeEventActionBlock;
        readonly ActionBlock<BulkUploadFileOperation> _uploadFileActionBlock;
        readonly INodeIdentityProvider _nodeIdentityProvider;
        readonly Metadata _headers;
        readonly IServiceProvider _serviceProvider;
        readonly IDisposable? _serverOptionsMonitorToken;
        readonly ServiceOptions _serviceHostOptions;
        long _heartBeatCounter;
        long _cancellationCounter;
        ServerOptions _serverOptions;

        public ILogger<NodeClientService> _logger { get; private set; }


        public NodeClientService(
            ILogger<NodeClientService> logger,
            IServiceProvider serviceProvider,
            IAsyncQueue<TaskExecutionContext> taskExecutionContextQueue,
            IAsyncQueue<TaskExecutionReport> taskReportQueue,
            [FromKeyedServices(nameof(NodeClientService))]IAsyncQueue<FileSystemWatchEventReport> fileSystemWatchEventQueue,
            [FromKeyedServices(nameof(NodeFileSystemWatchService))]IAsyncQueue<BatchQueueOperation<FileSystemWatchConfigModel,bool>> fileSystemConfigurationQueue,
            TaskExecutionContextDictionary taskExecutionContextDictionary,
            INodeIdentityProvider nodeIdentityProvider,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor,
            ServiceOptions serviceHostOptions
            )
        {
            _serviceProvider = serviceProvider;
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
            _taskExecutionContextQueue = taskExecutionContextQueue;
            _fileSystemWatchEventQueue = fileSystemWatchEventQueue;
            _taskExecutionReportQueue = taskReportQueue;
            _fileSystemConfigurationQueue = fileSystemConfigurationQueue;
            _logger = logger;
            _subscribeEventActionBlock = new ActionBlock<SubscribeEventInfo>(ProcessSubscribeEventAsync,
            new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                EnsureOrdered = true,
            });

            _uploadFileActionBlock = new ActionBlock<BulkUploadFileOperation>(ProcessUploadFileAsync,
            new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : 8,
            });
            _nodeIdentityProvider = nodeIdentityProvider;
            _headers = new Metadata();
            _serverOptions = serverOptionsMonitor.CurrentValue;
            _serverOptionsMonitorToken = serverOptionsMonitor.OnChange(OnServerOptionsChanged);
            _serviceHostOptions = serviceHostOptions;
        }

        private void OnServerOptionsChanged(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
        }

        public override void Dispose()
        {
            if (_serverOptionsMonitorToken != null)
            {
                _serverOptionsMonitorToken.Dispose();
            }
            base.Dispose();
        }

        async Task ProcessSubscribeEventAsync(SubscribeEventInfo subscribeEventInfo)
        {
            try
            {
                await ProcessSubscribeEventAsync(subscribeEventInfo.Client,
                                                 subscribeEventInfo.SubscribeEvent,
                                                 subscribeEventInfo.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }



        protected async override Task ExecuteAsync(CancellationToken cancellationToken = default)
        {

            try
            {

                _headers.AppendNodeClientHeaders(new NodeClientHeaders()
                {
                    HostName = Dns.GetHostName(),
                    NodeId = Debugger.IsAttached ? "DebugMachine" : _nodeIdentityProvider.GetIdentity(),
                    Mode = _serviceHostOptions.mode
                });
                await RunLoopAsync(cancellationToken);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        async ValueTask RunLoopAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                CancellationTokenRegistration cancellationTokenRegistration = default;
                using var cancellationTokenSource = new CancellationTokenSource();
                try
                {

                    cancellationTokenRegistration = cancellationToken.Register(cancellationTokenSource.Cancel);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!cancellationTokenSource.IsCancellationRequested)
                            {
                                var heartBeatCounter1 = GetHeartBeatCounter();
                                _logger.LogInformation($"HeartBeatCouner1:{heartBeatCounter1}");
                                await Task.Delay(TimeSpan.FromMinutes(10), cancellationTokenSource.Token);
                                if (cancellationTokenSource.IsCancellationRequested)
                                {
                                    return;
                                }
                                var heartBeatCounter2 = GetHeartBeatCounter();
                                _logger.LogInformation($"HeartBeatCouner2:{heartBeatCounter2}");
                                if (heartBeatCounter2 == heartBeatCounter1)
                                {
                                    _cancellationCounter++;
                                    cancellationTokenSource.Cancel();
                                    _logger.LogInformation($"Cancel:CancellationCounter{_cancellationCounter},HeartBeatCounter:{heartBeatCounter1}");
                                    cancellationTokenRegistration.Unregister();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation(ex.ToString());
                        }
                    }, cancellationToken);
                    await RunTasksAsync(cancellationTokenSource.Token);

                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.ToString());
                }
                finally
                {
                    cancellationTokenRegistration.Unregister();
                    try
                    {
                        if (!cancellationTokenSource.IsCancellationRequested)
                        {
                            cancellationTokenSource.Cancel();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }

                }

            }

        }

        HttpMessageHandler GetHttpMessageHandler(HttpClientHandler httpClientHandler)
        {
            _logger.LogInformation($"OperationSystem:{Environment.OSVersion}");
            if (OperatingSystem.IsWindows())
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 0, 0))
                {
                    return httpClientHandler;
                }
                return new GrpcWebHandler(httpClientHandler);
            }
            return httpClientHandler;
        }

        async ValueTask RunTasksAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var httpClientHandler = new HttpClientHandler();
                httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                _logger.LogInformation($"Grpc Address:{_serverOptions.GrpcAddress}");

                var grpcChannelOptions = new GrpcChannelOptions()
                {
                    HttpHandler = GetHttpMessageHandler(httpClientHandler),
                    Credentials = ChannelCredentials.SecureSsl,
                    ServiceProvider = _serviceProvider,
                    DisposeHttpClient = true
                };

                using var grpcChannel = GrpcChannel.ForAddress(_serverOptions.GrpcAddress, grpcChannelOptions);

                var nodeServiceClient = new NodeServiceClient(grpcChannel);
                var tasks = EnumTasks(nodeServiceClient, cancellationToken).ToArray();
                await Task.WhenAny(tasks);
                await Task.Delay(TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            finally
            {

            }

        }

        IEnumerable<Task> EnumTasks(NodeServiceClient nodeServiceClient, CancellationToken cancellationToken = default)
        {
            yield return SubscribeAsync(nodeServiceClient, cancellationToken);
            yield return SendFileSystemEventReportAsync(nodeServiceClient, cancellationToken);
            yield return SendTaskExecutionReportAsync(nodeServiceClient, cancellationToken);
            yield return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        async Task SubscribeAsync(NodeServiceClient nodeServiceClient, CancellationToken cancellationToken = default)
        {
            try
            {
                using var subscribeCall = nodeServiceClient.Subscribe(new SubscribeRequest()
                {
                    RequestId = Guid.NewGuid().ToString(),
                }, _headers, cancellationToken: cancellationToken);
                await foreach (var subscribeEvent in subscribeCall.ResponseStream.ReadAllAsync(cancellationToken))
                {
                    _logger.LogInformation(subscribeEvent.ToString());
                    _subscribeEventActionBlock.Post(new SubscribeEventInfo()
                    {
                        Client = nodeServiceClient,
                        SubscribeEvent = subscribeEvent,
                        CancellationToken = cancellationToken,
                    });
                }
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        async Task SendFileSystemEventReportAsync(NodeServiceClient nodeServiceClient, CancellationToken cancellationToken=default)
        {
            try
            {

                using var fileSystemWatchEventReportStreamingCall = nodeServiceClient.SendFileSystemWatchEventReport(
                    _headers,
                    cancellationToken: cancellationToken);
                Stopwatch stopwatch = new Stopwatch();
                while (!cancellationToken.IsCancellationRequested)
                {
                    int messageCount = 0;
                    stopwatch.Start();
                    while (!cancellationToken.IsCancellationRequested && await _fileSystemWatchEventQueue.WaitToReadAsync(cancellationToken))
                    {
                        if (!_fileSystemWatchEventQueue.TryPeek(out var reportMessage))
                        {
                            continue;
                        }
                        await fileSystemWatchEventReportStreamingCall.RequestStream.WriteAsync(
                            reportMessage,
                            cancellationToken);
                        await _fileSystemWatchEventQueue.DeuqueAsync(cancellationToken);
                        if (Debugger.IsAttached)
                        {
                            _logger.LogInformation(reportMessage.ToString());
                        }
                        messageCount++;
                    }
                    stopwatch.Stop();
                    if (messageCount > 0)
                    {
                        _logger.LogInformation($"Sent {messageCount} {nameof(FileSystemWatchEventReport)} messages,spent:{stopwatch.Elapsed}");
                    }
                    stopwatch.Reset();
                }

            }
            catch (RpcException ex)
            {
                _logger.LogError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        async Task SendTaskExecutionReportAsync(NodeServiceClient nodeServiceClient, CancellationToken cancellationToken = default)
        {
            try
            {
                using var taskReportStreamingCall = nodeServiceClient.SendTaskExecutionReport(
                            _headers,
                            cancellationToken: cancellationToken);

                Stopwatch stopwatch = new Stopwatch();
                while (!cancellationToken.IsCancellationRequested)
                {
                    int messageCount = 0;
                    stopwatch.Start();
                    while (!cancellationToken.IsCancellationRequested && await _taskExecutionReportQueue.WaitToReadAsync(cancellationToken))
                    {
                        if (!_taskExecutionReportQueue.TryPeek(out var taskExecutionReport))
                        {
                            continue;
                        }
                        await taskReportStreamingCall.RequestStream.WriteAsync(
                            taskExecutionReport,
                            cancellationToken);
                        await _taskExecutionReportQueue.DeuqueAsync(cancellationToken);
                        messageCount++;
                    }
                    stopwatch.Stop();
                    if (messageCount > 0)
                    {
                        _logger.LogInformation($"Sent {messageCount} {nameof(TaskExecutionReport)} messages,spent:{stopwatch.Elapsed}");
                    }
                    stopwatch.Reset();
                }

            }
            catch (RpcException ex)
            {
                _logger.LogError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        async ValueTask ProcessSubscribeEventAsync(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            switch (subscribeEvent.EventCase)
            {
                case SubscribeEvent.EventOneofCase.None:
                    break;
                case SubscribeEvent.EventOneofCase.HeartBeatRequest:
                    await ProcessHeartBeatRequest(
                        client,
                        subscribeEvent,
                        cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemListDirectoryRequest:
                    await ProcessFileSystemListDirectoryRequest(
                        client,
                        subscribeEvent,
                        cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemListDriveRequest:
                    await ProcessFileSystemListDriveRequest(
                        client,
                        subscribeEvent,
                        cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemBulkOperationRequest:
                    await ProcessFileSystemBulkOperationRequest(
                        client,
                        subscribeEvent,
                        cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.ConfigurationChangedReport:
                    await ProcessConfigurationChangedReportAsync(
                        client,
                        subscribeEvent,
                        cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.TaskExecutionEventRequest:
                    await ProcessTaskExecutionEventRequest(
                        client,
                        subscribeEvent,
                        cancellationToken);
                    break;
                default:
                    break;
            }
        }

    }
}
