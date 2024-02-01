using JobsWorker.Shared.Models;
using JobsWorkerNodeService.Jobs;
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

        public JobScheduleConfig? Config { get; private set; }

        public async Task ScheduleAsync(JobScheduleConfig jobScheduleConfiguration)
        {
            try
            {
                this.Logger.LogInformation($"ScheduleAsync:{JsonSerializer.Serialize(jobScheduleConfiguration)}");

                var type = Type.GetType(jobScheduleConfiguration.jobType);

                IDictionary<string, object> props = new Dictionary<string, object>()
                {
                    {nameof(JobScheduleConfig.options),jobScheduleConfiguration.options },
                    {nameof(Logger),this.Logger},
                };

                IJobDetail job = JobBuilder.Create(type)
                    .SetJobData(new JobDataMap(props))
                        .Build();

                if (jobScheduleConfiguration.executeNow)
                {
                    var trigger = TriggerBuilder.Create()
                        .StartNow()
                        .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
                        .Build();

                    await this._scheduler.ScheduleJob(job, trigger);
                    this.Logger.LogInformation($"ScheduleJobs:{jobScheduleConfiguration.jobName},TriggerTimes:now");
                    this._jobKey = job.Key;
                }

                Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>> jobsAndTriggers = [];

                var triggers = jobScheduleConfiguration.cronExpressions.Select(x => TriggerBuilder.Create()
                    .WithCronSchedule(x.Trim())
                    .Build())
                    .ToList()
                    .AsReadOnly();

                jobsAndTriggers.Add(job, triggers);
                await this._scheduler.ScheduleJobs(jobsAndTriggers, true);
                this._jobKey = job.Key;
                this.Logger.LogInformation($"ScheduleJobs:{jobScheduleConfiguration.jobName},JobKey:{this._jobKey},TriggerTimes:{string.Join(",", jobScheduleConfiguration.cronExpressions)}");
                this.Config = jobScheduleConfiguration;
                this.JobId = jobScheduleConfiguration.jobId;
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
