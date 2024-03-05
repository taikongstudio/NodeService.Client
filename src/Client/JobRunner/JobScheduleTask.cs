using JobsWorker.Shared.DataModels;
using JobsWorkerNodeService.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace JobsWorkerNodeService.Models
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

        public string JobId { get; set; }

        public JobScheduleConfigModel? Config { get; private set; }

        public async Task ScheduleAsync(JobScheduleConfigModel jobScheduleConfig, NodeConfigTemplateModel nodeConfig)
        {
            try
            {
                this.Logger.LogInformation($"ScheduleAsync:{JsonSerializer.Serialize(jobScheduleConfig)}");
                var jobBaseType = typeof(JobBase);
                var type = jobBaseType.Assembly.GetType(jobScheduleConfig.JobTypeDesc);
                if (type == null || type.BaseType != jobBaseType)
                {
                    throw new InvalidOperationException($"invalid job type:{jobScheduleConfig.JobTypeDesc}");
                }

                IDictionary<string, object> props = new Dictionary<string, object>()
                {
                    {nameof(JobBase.NodeConfig),nodeConfig },
                    {nameof(JobBase.Logger),this.Logger},
                    {nameof(JobBase.JobScheduleConfig),jobScheduleConfig},
                };

                IJobDetail job = JobBuilder.Create(type)
                    .SetJobData(new JobDataMap(props))
                        .Build();

                Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>> jobsAndTriggers = [];

                var triggers = jobScheduleConfig.CronExpressions.Select(x => TriggerBuilder.Create()
                    .WithCronSchedule(x.Trim())
                    .Build())
                    .ToList()
                    .AsReadOnly();

                jobsAndTriggers.Add(job, triggers);
                await this._scheduler.ScheduleJobs(jobsAndTriggers, true);
                this._jobKey = job.Key;
                this.Logger.LogInformation($"ScheduleJobs:{jobScheduleConfig.Name},JobKey:{this._jobKey},TriggerTimes:{string.Join(",", jobScheduleConfig.CronExpressions)}");
                this.Config = jobScheduleConfig;
                this.JobId = jobScheduleConfig.Id;
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
                this.Logger.LogInformation($"DeleteJob:{this._jobKey}");
            }
        }

    }
}
