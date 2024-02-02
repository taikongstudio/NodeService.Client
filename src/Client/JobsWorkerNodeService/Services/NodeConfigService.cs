
using JobsWorker.Shared;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.Models;
using JobsWorkerNodeService.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobsWorkerNodeService.Services
{
    public class NodeConfigService : BackgroundService
    {
        private readonly IInprocMessageQueue<string, string, NodeConfigChangedEvent> _inprocMessageQueue;
        private readonly ActionBlock<NodeConfigChangedEvent> _nodeConfigUpdateEventActionBlock;
        private readonly IConfigurationStore _configurationStore;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ConcurrentDictionary<string, JobScheduleTask> _taskDict;

        public ILogger<NodeConfigService> Logger { get; private set; }

        public NodeConfigService(
            IInprocMessageQueue<string, string, NodeConfigChangedEvent> messageQueue,
            IConfigurationStore configurationStore,
            ILogger<NodeConfigService> logger,
            ISchedulerFactory schedulerFactory)
        {
            this.Logger = logger;
            this._schedulerFactory = schedulerFactory;
            this._configurationStore = configurationStore;
            this._inprocMessageQueue = messageQueue;
            this._nodeConfigUpdateEventActionBlock = new ActionBlock<NodeConfigChangedEvent>(ProcessUpdatedEventAsync,
                new ExecutionDataflowBlockOptions()
                {

                    MaxDegreeOfParallelism = 1
                }
                );
            this._taskDict = new ConcurrentDictionary<string, JobScheduleTask>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                this.Logger.LogInformation($"Start {nameof(NodeConfigService)}");
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await this.ProcessNodeConfigUpdateEventAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex.ToString());
                    }
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

        private async Task ProcessNodeConfigUpdateEventAsync(CancellationToken cancellationToken = default)
        {
            try
            {

                await foreach (var updateEvent in
                    this._inprocMessageQueue
                    .ReadAllMessageAsync<NodeConfigChangedEvent>(
                        nameof(NodeConfigService), null, cancellationToken))
                {
                    updateEvent.CancellationToken = cancellationToken;
                    this.Logger.LogInformation($"Pending:{updateEvent.ToJsonString()}");
                    this._nodeConfigUpdateEventActionBlock.Post(updateEvent);
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
            finally
            {

            }
        }

        private async Task ProcessUpdatedEventAsync(NodeConfigChangedEvent updateEvent)
        {
            try
            {
                if (this._configurationStore.NodeConfig == null)
                {
                    this._configurationStore.NodeConfig = updateEvent.Content;
                }
                this.Logger.LogInformation($"Processing:{updateEvent.ToJsonString()}");
                var scheduler = await this._schedulerFactory.GetScheduler();
                NodeConfigInstaller nodeConfigInstaller = new NodeConfigInstaller(this.Logger, scheduler, this._taskDict);
                while (true)
                {
                    if (await nodeConfigInstaller.InstallAsync(this._configurationStore.NodeConfig))
                    {
                        break;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)));
                }
                this.Logger.LogInformation($"Installed:{updateEvent.ToJsonString()}");
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
        }






    }
}
