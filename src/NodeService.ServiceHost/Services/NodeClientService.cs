using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client.Web;
using NodeService.Infrastructure.Concurrent;
using NodeService.ServiceHost.Models;
using System.Net.Http;
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

        readonly ActionBlock<SubscribeEventInfo> _subscribeEventActionBlock;
        readonly INodeIdentityProvider _nodeIdentityProvider;
        readonly Metadata _headers;
        readonly NodeServiceClient _nodeServiceClient;
        readonly IServiceProvider _serviceProvider;
        readonly IDisposable? _serverOptionsMonitorToken;
        readonly ServiceOptions _serviceHostOptions;
        public ILogger<NodeClientService> _logger { get; private set; }


        public NodeClientService(
            ILogger<NodeClientService> logger,
            IServiceProvider serviceProvider,
            IAsyncQueue<TaskExecutionContext> taskExecutionContextQueue,
            IAsyncQueue<TaskExecutionReport> taskReportQueue,
            TaskExecutionContextDictionary taskExecutionContextDictionary,
            INodeIdentityProvider nodeIdentityProvider,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor,
            ServiceOptions serviceHostOptions,
            NodeServiceClient nodeServiceClient
            )
        {

            _nodeServiceClient = nodeServiceClient;
            _serviceProvider = serviceProvider;
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
            _taskExecutionContextQueue = taskExecutionContextQueue;
            _taskExecutionReportQueue = taskReportQueue;
            _logger = logger;
            _subscribeEventActionBlock = new ActionBlock<SubscribeEventInfo>(ProcessSubscribeEventAsync,
            new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                EnsureOrdered = true,
            });

            _nodeIdentityProvider = nodeIdentityProvider;
            _headers = [];
            _serviceHostOptions = serviceHostOptions;
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
                try
                {
                    await RunTasksAsync(cancellationToken);

                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.ToString());
                }
                finally
                {


                }

            }

        }



        async ValueTask RunTasksAsync(CancellationToken cancellationToken = default)
        {
            try
            {

                _logger.LogInformation($"OperationSystem:{Environment.OSVersion}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var tasks = EnumTasks(cancellationToken).ToArray();
                    await Task.WhenAll(tasks);
                    await Task.Delay(TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            finally
            {

            }

        }

        IEnumerable<Task> EnumTasks(CancellationToken cancellationToken = default)
        {
            yield return SubscribeAsync(cancellationToken);
            yield return SendTaskExecutionReportAsync(cancellationToken);
        }

        async Task SubscribeAsync(CancellationToken cancellationToken = default)
        {
            try
            {

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using var subscribeCall = _nodeServiceClient.Subscribe(new SubscribeRequest()
                        {
                            RequestId = Guid.NewGuid().ToString(),
                        }, _headers, cancellationToken: cancellationToken);
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
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                    }
                    finally
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }

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

        async Task SendTaskExecutionReportAsync(CancellationToken cancellationToken = default)
        {
            try
            {


                var stopwatch = new Stopwatch();
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using var taskReportStreamingCall = _nodeServiceClient.SendTaskExecutionReport(
                            _headers,
                            cancellationToken: cancellationToken);
                        int messageCount = 0;
                        stopwatch.Restart();
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
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                    }
                    finally
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }

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
