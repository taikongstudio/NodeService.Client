

using AntDesign;
using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.MessageQueues;

namespace JobsWorkerWebService.BackgroundServices
{
    internal class JobScheduleService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobScheduleService> Logger;
        private readonly ISchedulerFactory SchedulerFactory;
        private IScheduler Scheduler;

        private readonly GlobalNodeTaskDictionary _taskDict;

        public JobScheduleService(
            IServiceProvider serviceProvider,
            ILogger<JobScheduleService> logger,
            ISchedulerFactory schedulerFactory)
        {
            this._serviceProvider = serviceProvider;
            this.Logger = logger;
            this.SchedulerFactory = schedulerFactory;
            this._taskDict = this._serviceProvider.GetKeyedService<GlobalNodeTaskDictionary>("GlobalNodeTaskDictionary");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.Scheduler = await SchedulerFactory.GetScheduler();
            await this.Scheduler.Start();
            await ScheduleJobsFromDbContext();
            var messageQueue = this._serviceProvider.GetKeyedService<NodeConfigTemplateNotificationMessageQueue>(nameof(JobScheduleService));
            while (!stoppingToken.IsCancellationRequested)
            {
                await foreach (var nodeConfigChangedMessage in
                    messageQueue
                    .ReadAllMessageAsync<NodeConfigTemplateNotificationMessage>(nameof(JobScheduleService), null, stoppingToken))
                {
                    try
                    {
                        using var serviceScope = this._serviceProvider.CreateAsyncScope();
                        using var applicationDbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        if (nodeConfigChangedMessage.Content == null)
                        {
                            continue;
                        }
                        var nodeConfigTemplate = await applicationDbContext.NodeConfigTemplateDbSet.FindAsync(nodeConfigChangedMessage.Content);
                        if (nodeConfigTemplate == null)
                        {
                            continue;
                        }
                        await this.ScheduleNodeConfigTeamplateJobsAsync(applicationDbContext, nodeConfigTemplate);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex.ToString());
                    }

                }
            }
        }

        private async Task ScheduleNodeConfigTeamplateJobsAsync(ApplicationDbContext applicationDbContext, NodeConfigTemplateModel nodeConfigTemplate)
        {
            await applicationDbContext.LoadAsync(nodeConfigTemplate);
            await this.ScheduleJobsAsync(nodeConfigTemplate.JsonClone<NodeConfigTemplateModel>());
        }

        private async Task ScheduleJobsFromDbContext()
        {
            try
            {
                using var serviceScope = this._serviceProvider.CreateAsyncScope();
                using var applicationDbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var nodeConfigTemplateGroups = await applicationDbContext.NodeInfoDbSet.GroupBy(x => x.ActiveNodeConfigTemplateForeignKey).ToListAsync();
                foreach (var nodeConfigTemplateGroup in nodeConfigTemplateGroups)
                {
                    try
                    {
                        if (nodeConfigTemplateGroup.Key == null)
                        {
                            continue;
                        }
                        NodeConfigTemplateModel? nodeConfigTemplate =
                            await applicationDbContext
                            .NodeConfigTemplateDbSet
                            .FindAsync(nodeConfigTemplateGroup.Key);

                        if (nodeConfigTemplate == null)
                        {
                            continue;
                        }
                        await ScheduleNodeConfigTeamplateJobsAsync(applicationDbContext, nodeConfigTemplate);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex.ToString());
                    }


                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

        private async Task ScheduleJobsAsync(
            NodeConfigTemplateModel nodeConfigTemplate)
        {
            var taskDictionary = GetTaskDictionary(nodeConfigTemplate.Id);
            var removeList = await this.UpdateTaskDictionaryAsync(
                nodeConfigTemplate,
                taskDictionary,
                nodeConfigTemplate.JobScheduleConfigs,
                AddJobAsync,
                UpdateJobFunc,
                CancelJobAsync
                );
        }

        private ValueTask<JobScheduleTask> AddJobAsync(string key, NodeConfigTemplateModel nodeConfigTemplate)
        {
            var jobScheduleConfig = nodeConfigTemplate.JobScheduleConfigs.FirstOrDefault(x => x.Id == key);
            return ScheduleJobAsync(nodeConfigTemplate, jobScheduleConfig);
        }

        private async ValueTask<JobScheduleTask?> UpdateJobFunc(string key, JobScheduleTask oldJob, NodeConfigTemplateModel nodeConfigTemplate)
        {
            var jobScheduleConfig = nodeConfigTemplate.JobScheduleConfigs.FirstOrDefault(x => x.Id == key);
            if (jobScheduleConfig == null)
            {
                return oldJob;
            }
            if (jobScheduleConfig.ToJsonString<JobScheduleConfigModel>() == oldJob.JobScheduleConfig.ToJsonString<JobScheduleConfigModel>())
            {
                return oldJob;
            }
            await oldJob.CancelAsync();
            return await ScheduleJobAsync(nodeConfigTemplate, jobScheduleConfig);
        }

        private async ValueTask<JobScheduleTask?> ScheduleJobAsync(NodeConfigTemplateModel nodeConfigTemplate, JobScheduleConfigModel? jobScheduleConfig)
        {
            var jobScheduleTask = new JobScheduleTask(this.Logger, this.Scheduler);
            await jobScheduleTask.ScheduleAsync(nodeConfigTemplate, jobScheduleConfig, this._serviceProvider);
            return jobScheduleTask;
        }

        private ValueTask CancelJobAsync(JobScheduleTask jobScheduleTask)
        {
            return new ValueTask(jobScheduleTask.CancelAsync());
        }

        public async Task<IEnumerable<JobScheduleTask>> UpdateTaskDictionaryAsync(
            NodeConfigTemplateModel nodeConfigTemplateModel,
            ConcurrentDictionary<string, JobScheduleTask> dest,
            IEnumerable<JobScheduleConfigModel> src,
            Func<string, NodeConfigTemplateModel, ValueTask<JobScheduleTask>> addValueFactory,
            Func<string, JobScheduleTask, NodeConfigTemplateModel, ValueTask<JobScheduleTask>> updateValueFactory,
            Func<JobScheduleTask, ValueTask> removeFunc)
        {
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));
            ArgumentNullException.ThrowIfNull(src, nameof(src));

            if (dest.IsEmpty && !src.Any())
            {
                return [];
            }

            List<KeyValuePair<string, JobScheduleTask>> removeList = [];

            foreach (var item in dest)
            {
                if (!src.Any(y => y.Id == item.Key))
                {
                    removeList.Add(item);
                }
            }
            foreach (var item in removeList)
            {
                await removeFunc.Invoke(item.Value);
                dest.TryRemove(item);
            }
            foreach (var item in src)
            {
                if (!dest.TryGetValue(item.Id, out var value))
                {
                    var addedValue = await addValueFactory(item.Id, nodeConfigTemplateModel);
                    dest.TryAdd(item.Id, addedValue);
                }
                else
                {
                    var updatedValue = await updateValueFactory(item.Id, value, nodeConfigTemplateModel);
                    if (updatedValue == null)
                    {
                        continue;
                    }
                    dest.TryUpdate(item.Id, updatedValue, value);
                }
            }
            return removeList.Select(x => x.Value);
        }

        private ConcurrentDictionary<string, JobScheduleTask> GetTaskDictionary(string key)
        {
            if (!this._taskDict.TryGetValue(key, out ConcurrentDictionary<string, JobScheduleTask>? taskDictionary))
            {
                taskDictionary = [];
                this._taskDict.TryAdd(key, taskDictionary);
            }
            return taskDictionary;
        }

    }
}
