﻿using JobsWorkerDaemonService.Jobs;
using JobsWorkerDaemonService.Models;
using JobsWorkerDaemonService.Workers;
using Quartz;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorkerDaemonService
{
    public class JobScheduleTask
	{
		private readonly IScheduler _scheduler;
		private readonly ILogger _logger;

		private JobKey _jobKey;

		public JobScheduleTask(ILogger logger, IScheduler scheduler)
		{
			_logger = logger;
			_scheduler = scheduler;
		}

		public async Task ScheduleAsync(JobScheduleConfig jobScheduleConfig)
		{
			try
			{
				this._logger.LogInformation($"ScheduleAsync:{JsonSerializer.Serialize(jobScheduleConfig)}");

				var type = Type.GetType(jobScheduleConfig.jobName);

				IDictionary<string, object> props = new Dictionary<string, object>()
			{
				{nameof(JobScheduleConfig.arguments),jobScheduleConfig.arguments },
				{nameof(JobBase.Logger),this._logger},
			};

				IJobDetail job = JobBuilder.Create(type)
					.SetJobData(new JobDataMap(props))
						.Build();

				if (jobScheduleConfig.executeNow)
				{
					var trigger = TriggerBuilder.Create()
						.StartNow()
						.WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
						.Build();

					await _scheduler.ScheduleJob(job, trigger);
					this._logger.LogInformation($"ScheduleJobs:{jobScheduleConfig.jobName},TriggerTimes:now");
					this._jobKey = job.Key;
				}
				else
				{
					Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>> jobsAndTriggers =
	new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>();

					var triggers = jobScheduleConfig.cronExpressions.Select(x => TriggerBuilder.Create()
						.WithCronSchedule(x.Trim())
						.Build())
						.ToList()
						.AsReadOnly();

					jobsAndTriggers.Add(job, triggers);
					await _scheduler.ScheduleJobs(jobsAndTriggers, true);
					this._jobKey = job.Key;
					this._logger.LogInformation($"ScheduleJobs:{jobScheduleConfig.jobName},TriggerTimes:{string.Join(",", jobScheduleConfig.cronExpressions)}");

				}

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
				await _scheduler.DeleteJob(_jobKey);
				this._logger.LogInformation($"Cancel {this._jobKey}");
			}
		}

	}
}
