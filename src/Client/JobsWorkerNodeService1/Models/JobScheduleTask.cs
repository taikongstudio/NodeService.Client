
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JobsWorkerNodeService.Jobs;
using JobsWorkerWebService.Models;

namespace JobsWorkerNodeService.Models
{
    public class JobScheduleTask
    {
        private readonly IScheduler _scheduler;
        private readonly ILogger _logger;

        private JobKey _jobKey;

        public JobScheduleTask(ILogger logger, IScheduler scheduler)
        {
            this._logger = logger;
            this._scheduler = scheduler;
        }

        public string JobId { get; set; }

        public JobScheduleConfiguration? Configuration { get; private set; }

        public async Task ScheduleAsync(JobScheduleConfiguration jobScheduleConfiguration)
        {
            try
            {
                this._logger.LogInformation($"ScheduleAsync:{JsonSerializer.Serialize(jobScheduleConfiguration)}");

                var type = Type.GetType(jobScheduleConfiguration.jobType);

                IDictionary<string, object> props = new Dictionary<string, object>()
                {
                    {nameof(JobScheduleConfiguration.arguments),jobScheduleConfiguration.arguments },
                    {nameof(JobBase.Logger),this._logger},
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
                    this._logger.LogInformation($"ScheduleJobs:{jobScheduleConfiguration.jobName},TriggerTimes:now");
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
                this._logger.LogInformation($"ScheduleJobs:{jobScheduleConfiguration.jobName},JobKey:{this._jobKey},TriggerTimes:{string.Join(",", jobScheduleConfiguration.cronExpressions)}");
                this.Configuration = jobScheduleConfiguration;

            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }

        }

        public async Task CancelAsync()
        {
            if (this._jobKey != null)
            {
                await this._scheduler.DeleteJob(_jobKey);
                this._logger.LogInformation($"DeleteJob:{this._jobKey}");
            }
        }

    }
}
