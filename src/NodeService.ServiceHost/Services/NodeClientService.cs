using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using NodeService.Infrastructure.NodeSessions;
using NodeService.ServiceHost.Models;
using NodeService.ServiceHost.Services;
using System.Net;
using System.Threading.Tasks.Dataflow;
using static NodeService.Infrastructure.Services.NodeService;

namespace NodeService.WindowsService.Services
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
        private NodeServiceClient _nodeServiceClient;
        private ServerOptions _serverOptions;
        private readonly IDisposable? _serverOptionsMonitorToken;
        private readonly ServiceOptions _serviceHostOptions;

        public ILogger<NodeClientService> _logger { get; private set; }


        public NodeClientService(
            IServiceProvider serviceProvider,
            ILogger<NodeClientService> logger,
            IAsyncQueue<TaskExecutionContext> taskExecutionContextQueue,
            IAsyncQueue<JobExecutionReport> reportQueue,
            TaskExecutionContextDictionary taskExecutionContextDictionary,
            INodeIdentityProvider nodeIdentityProvider,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor,
            ServiceOptions serviceHostOptions
            )
        {
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
            _serviceProvider = serviceProvider;
            _taskExecutionContextQueue = taskExecutionContextQueue;
            _reportQueue = reportQueue;
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
            _logger = logger;
            _subscribeEventActionBlock = new ActionBlock<SubscribeEventInfo>(ProcessSubscribeEventAsync, new ExecutionDataflowBlockOptions()
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
                    await RunGrpcLoopAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task RunGrpcLoopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var httpClientHandler = new HttpClientHandler();
                httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                var dnsName = Dns.GetHostName();
                _logger.LogInformation($"Grpc Address:{_serverOptions.GrpcAddress}");

                using var channel = GrpcChannel.ForAddress(_serverOptions.GrpcAddress, new GrpcChannelOptions()
                {
                    HttpHandler = httpClientHandler,
                    Credentials = ChannelCredentials.SecureSsl,
                    ServiceProvider = _serviceProvider,
                });

                _nodeServiceClient = new NodeServiceClient(channel);
                using var subscribeCall = _nodeServiceClient.Subscribe(new SubscribeRequest()
                {
                    RequestId = Guid.NewGuid().ToString(),
                }, _headers, cancellationToken: cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {

                        using var reportStreamingCall = _nodeServiceClient.SendJobExecutionReport(_headers, cancellationToken: cancellationToken);
                        Stopwatch stopwatch = new Stopwatch();
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            int messageCount = 0;
                            stopwatch.Start();
                            while (!cancellationToken.IsCancellationRequested && await _reportQueue.WaitToReadAsync(cancellationToken))
                            {
                                if (!_reportQueue.TryPeek(out var reportMessage))
                                {
                                    continue;
                                }
                                await reportStreamingCall.RequestStream.WriteAsync(reportMessage, cancellationToken);
                                await _reportQueue.DeuqueAsync(cancellationToken);
                                messageCount++;
                            }
                            stopwatch.Stop();
                            if (messageCount > 0)
                            {
                                _logger.LogInformation($"Sent {messageCount} messages,spent:{stopwatch.Elapsed}");
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
                        Client = _nodeServiceClient,
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
                await Task.Delay(TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30), cancellationToken);
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
                    await ProcessHeartBeatRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemListDirectoryRequest:
                    await ProcessFileSystemListDirectoryRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemListDriveRequest:
                    await ProcessFileSystemListDriveRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemBulkOperationRequest:
                    await ProcessFileSystemBulkOperationRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.ConfigurationChangedReport:
                    //await ProcessConfigurationChangedReport(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.JobExecutionEventRequest:
                    await ProcessJobExecutionEventRequest(client, subscribeEvent, cancellationToken);
                    break;
                default:
                    break;
            }
        }


    }
}
