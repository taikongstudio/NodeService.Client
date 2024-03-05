
//using JobsWorker.Shared;
//using JobsWorker.Shared.MessageQueues;
//using JobsWorker.Shared.Models;
//using JobsWorkerNodeService.Models;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Quartz;
//using System.Collections.Concurrent;
//using System.Diagnostics;
//using System.IO;
//using System.IO.Compression;
//using System.Net;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Threading.Tasks.Dataflow;

//namespace JobsWorkerNodeService.Services
//{
//    public class NodeConfigService : BackgroundService
//    {
//        private readonly IInprocMessageQueue<string, string, NodeConfigChangedEvent> _inprocMessageQueue;
//        private readonly ActionBlock<NodeConfigChangedEvent> _nodeConfigUpdateEventActionBlock;
//        private readonly IConfigurationStore _configurationStore;
//        private readonly ISchedulerFactory _schedulerFactory;
//        private IScheduler _scheduler;
//        private readonly ConcurrentDictionary<string, JobRunnerScheduleTask> _taskDict;

//        public ILogger<NodeConfigService> Logger { get; private set; }

//        public NodeConfigService(
//            IInprocMessageQueue<string, string, NodeConfigChangedEvent> messageQueue,
//            IConfigurationStore configurationStore,
//            ILogger<NodeConfigService> logger,
//            ISchedulerFactory schedulerFactory)
//        {
//            this.Logger = logger;
//            this._schedulerFactory = schedulerFactory;
//            this._configurationStore = configurationStore;
//            this._inprocMessageQueue = messageQueue;
//            this._nodeConfigUpdateEventActionBlock = new ActionBlock<NodeConfigChangedEvent>(ProcessNodeConfigUpdatedEventAsync,
//                new ExecutionDataflowBlockOptions()
//                {

//                    MaxDegreeOfParallelism = 1
//                }
//                );
//            this._taskDict = new ConcurrentDictionary<string, JobRunnerScheduleTask>();
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            try
//            {
//                this._scheduler = await this._schedulerFactory.GetScheduler();
//                await this._scheduler.Start();
//                this.Logger.LogInformation($"Start {nameof(NodeConfigService)}");
//                while (!stoppingToken.IsCancellationRequested)
//                {
//                    try
//                    {
//                        await this.ProcessNodeConfigUpdateEventAsync(stoppingToken);
//                    }
//                    catch (Exception ex)
//                    {
//                        this.Logger.LogError(ex.ToString());
//                    }
//                    if (stoppingToken.IsCancellationRequested)
//                    {
//                        break;
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex.ToString());
//            }

//        }

//        private async Task ProcessNodeConfigUpdateEventAsync(CancellationToken cancellationToken = default)
//        {
//            try
//            {

//                await foreach (var updateEvent in
//                    this._inprocMessageQueue
//                    .ReadAllMessageAsync<NodeConfigChangedEvent>(
//                        nameof(NodeConfigService), null, cancellationToken))
//                {
//                    updateEvent.CancellationToken = cancellationToken;
//                    this.Logger.LogInformation($"Pending:{updateEvent.ToJsonString()}");
//                    this._nodeConfigUpdateEventActionBlock.Post(updateEvent);
//                }
//            }
//            catch (Exception ex)
//            {
//                this.Logger.LogError(ex.ToString());
//            }
//            finally
//            {

//            }
//        }

//        private async Task ProcessNodeConfigUpdatedEventAsync(NodeConfigChangedEvent nodeConfigChangedEvent)
//        {
//            try
//            {
//                var oldNodeConfig = this._configurationStore.NodeConfig;
//                var newNodeConfig = nodeConfigChangedEvent.Content;
//                if (oldNodeConfig?.AsJsonString() == newNodeConfig.AsJsonString())
//                {
//                    this.Logger.LogInformation($"Skip node config");
//                    return;
//                }

//                this.Logger.LogInformation($"Processing:{nodeConfigChangedEvent.ToJsonString()}");
//                this._configurationStore.IsNodeConfigInstalling = true;
//                NodeConfigInstaller nodeConfigInstaller = new NodeConfigInstaller(
//                    this.Logger,
//                    this._scheduler,
//                    oldNodeConfig,
//                    this._configurationStore);
//                bool isInstalled = false;
//                for (int i = 0; i < 5; i++)
//                {
//                    if (await nodeConfigInstaller.ApplyNodeConfigAsync(newNodeConfig))
//                    {
//                        isInstalled = true;
//                        break;
//                    }
//                    await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)));
//                }
//                if (isInstalled)
//                {
//                    this._configurationStore.NodeConfig = newNodeConfig;
//                    this.Logger.LogInformation($"Installed:{nodeConfigChangedEvent.ToJsonString()}");
//                    if (this._configurationStore.ActiveGrpcAddress != newNodeConfig.GrpcAddress)
//                    {
//                        this.Logger.LogInformation($"Grpc address has changed to {newNodeConfig.GrpcAddress},reconnecting");
//                        this._configurationStore.GrpcLoopCancellationTokenSource.Cancel();
//                    }
//                }

//                this._configurationStore.IsNodeConfigInstalling = false;

//            }
//            catch (Exception ex)
//            {
//                this.Logger.LogError(ex.ToString());
//            }
//        }






//    }
//}
