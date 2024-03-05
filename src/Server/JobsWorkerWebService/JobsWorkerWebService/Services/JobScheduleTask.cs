using JobsWorker.Shared.DataModels;
using JobsWorkerWebService.Jobs;

namespace JobsWorkerWebService.Services
{
    public class JobScheduleTask
    {
        private readonly IScheduler _scheduler;
        public ILogger Logger { get; private set; }

        private JobKey _jobKey;

        public JobScheduleTask(ILogger logger, IScheduler scheduler)
        {
            this.Logger = logger;
            this._scheduler = scheduler;
        }

        public string[] CronExpressions { get; private set; }

        public JobScheduleConfigModel JobScheduleConfig { get; private set; }

        public string Id { get; private set; }


        public async Task ScheduleAsync(NodeConfigTemplateModel nodeConfigTemplate, JobScheduleConfigModel jobScheduleConfig, IServiceProvider serviceProvider)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(nodeConfigTemplate, nameof(nodeConfigTemplate));
                ArgumentNullException.ThrowIfNull(jobScheduleConfig, nameof(jobScheduleConfig));
                ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));

                this.Logger.LogInformation($"ScheduleAsync:{jobScheduleConfig.ToJsonString<JobScheduleConfigModel>()}");
                var jobType = typeof(ExecuteJobInstanceJob);

                IDictionary<string, object> props = new Dictionary<string, object>()
                {
                    {nameof(ExecuteJobInstanceJob.Logger),serviceProvider.GetService<ILogger<ExecuteJobInstanceJob>>()},
                    {nameof(ExecuteJobInstanceJob.NodeConfigTemplate),nodeConfigTemplate.Clone<NodeConfigTemplateModel>()},
                    {nameof(ExecuteJobInstanceJob.JobScheduleConfig),jobScheduleConfig.Clone<JobScheduleConfigModel>()},
                    {nameof(ExecuteJobInstanceJob.ServiceProvider),serviceProvider},
                };

                IJobDetail job = JobBuilder.Create(jobType)
                    .SetJobData(new JobDataMap(props))
                        .Build();

                Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>> jobsAndTriggers = [];

                this.CronExpressions = jobScheduleConfig.CronExpressions.Select(x => x.Value).ToArray();

                var triggers = this.CronExpressions.Select(x => TriggerBuilder.Create()
                    .WithCronSchedule(x.Trim())
                    .Build())
                    .ToList()
                    .AsReadOnly();

                jobsAndTriggers.Add(job, triggers);
                await this._scheduler.ScheduleJobs(jobsAndTriggers, true);
                this.JobScheduleConfig = jobScheduleConfig;
                this._jobKey = job.Key;
                this.Logger.LogInformation($"ScheduleJobs:{jobScheduleConfig.Name},JobKey:{_jobKey},TriggerTimes:{string.Join(",", this.CronExpressions)}");
                this.Id = jobScheduleConfig.Id;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

        public async Task CancelAsync()
        {
            if (this._jobKey != null)
            {
                await this._scheduler.DeleteJob(_jobKey);
                this.Logger.LogInformation($"DeleteJob:{_jobKey}");
            }
        }

    }
}
