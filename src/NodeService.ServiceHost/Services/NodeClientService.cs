using Grpc.Net.Client.Web;
using NodeService.Infrastructure.Concurrent;
using NodeService.ServiceHost.Models;

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



        private readonly ActionBlock<SubscribeEventInfo> _subscribeEventActionBlock;
        private readonly ActionBlock<BulkUploadFileOperation> _uploadFileActionBlock;
        private readonly INodeIdentityProvider _nodeIdentityProvider;
        private readonly Metadata _headers;
        private readonly IServiceProvider _serviceProvider;
        private ServerOptions _serverOptions;
        private readonly IDisposable? _serverOptionsMonitorToken;
        private readonly ServiceOptions _serviceHostOptions;
        private long _heartBeatCounter;
        private long _cancellationCounter;

        public ILogger<NodeClientService> _logger { get; private set; }


        public NodeClientService(
            ILogger<NodeClientService> logger,
            IServiceProvider serviceProvider,
            IAsyncQueue<TaskExecutionContext> taskExecutionContextQueue,
            IAsyncQueue<JobExecutionReport> taskReportQueue,
            IAsyncQueue<FileSystemWatchEventReport> fileSystemWatchEventQueue,
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
            _taskReportQueue = taskReportQueue;
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
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
            InitializeFileSystemWatch();
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

        private async Task ProcessSubscribeEventAsync(SubscribeEventInfo subscribeEventInfo)
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



        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {

            try
            {

                _headers.AppendNodeClientHeaders(new NodeClientHeaders()
                {
                    HostName = Dns.GetHostName(),
                    NodeId = Debugger.IsAttached ? "DebugMachine" : _nodeIdentityProvider.GetIdentity(),
                    Mode = _serviceHostOptions.mode
                });

                while (!stoppingToken.IsCancellationRequested)
                {
                    CancellationTokenRegistration cancellationTokenRegistration = default;
                    try
                    {
                        using var cancellationTokenSource = new CancellationTokenSource();
                        cancellationTokenRegistration = stoppingToken.Register(cancellationTokenSource.Cancel);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                while (!cancellationTokenSource.IsCancellationRequested)
                                {
                                    var heartBeatCounter1 = GetHeartBeatCounter();
                                    _logger.LogInformation($"HeartBeatCouner1:{heartBeatCounter1}");
                                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
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

                        }, stoppingToken);
                        await RunGrpcLoopAsync(cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(ex.ToString());
                    }
                    finally
                    {
                        cancellationTokenRegistration.Unregister();
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
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

        private async Task RunGrpcLoopAsync(CancellationToken cancellationToken = default)
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
                };

                using var grpcChannel = GrpcChannel.ForAddress(_serverOptions.GrpcAddress, grpcChannelOptions);

                var nodeServiceClient = new NodeServiceClient(grpcChannel);
                using var subscribeCall = nodeServiceClient.Subscribe(new SubscribeRequest()
                {
                    RequestId = Guid.NewGuid().ToString(),
                }, _headers, cancellationToken: cancellationToken);



                _ = Task.Run(async () =>
                {
                    try
                    {

                        using var taskReportStreamingCall = nodeServiceClient.SendJobExecutionReport(
                            _headers,
                            cancellationToken: cancellationToken);
                        Stopwatch stopwatch = new Stopwatch();
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            int messageCount = 0;
                            stopwatch.Start();
                            while (!cancellationToken.IsCancellationRequested && await _taskReportQueue.WaitToReadAsync(cancellationToken))
                            {
                                if (!_taskReportQueue.TryPeek(out var reportMessage))
                                {
                                    continue;
                                }
                                await taskReportStreamingCall.RequestStream.WriteAsync(
                                    reportMessage,
                                    cancellationToken);
                                await _taskReportQueue.DeuqueAsync(cancellationToken);
                                messageCount++;
                            }
                            stopwatch.Stop();
                            if (messageCount > 0)
                            {
                                _logger.LogInformation($"Sent {messageCount} {nameof(JobExecutionReport)} messages,spent:{stopwatch.Elapsed}");
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


                }, cancellationToken: cancellationToken);

                _ = Task.Run(async () =>
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


                }, cancellationToken: cancellationToken);

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
                if (ex.StatusCode == StatusCode.Cancelled)
                {

                }
                await Task.Delay(
                    TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                await Task.Delay(
                    TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30),
                    cancellationToken);
            }
            finally
            {

            }

        }

        private async Task ProcessSubscribeEventAsync(
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
                case SubscribeEvent.EventOneofCase.JobExecutionEventRequest:
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
