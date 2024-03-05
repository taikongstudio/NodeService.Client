using JobsWorker.Shared.DataModels;
using JobsWorkerNodeService.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobRunner.Services
{
    internal class JobScheduleService : BackgroundService
    {
        private readonly ILogger<JobScheduleService> Logger;
        private readonly ISchedulerFactory SchedulerFactory;
        private IScheduler Scheduler;
        private readonly JobScheduleConfigModel JobScheduleConfig;
        private readonly NodeConfigTemplateModel NodeConfig;
        private readonly Options Options;

        public JobScheduleService(
            ILogger<JobScheduleService> logger,
            ISchedulerFactory schedulerFactory,
            Options options,
            JobScheduleConfigModel jobScheduleConfig,
            NodeConfigTemplateModel nodeConfig)
        {
            this.Logger = logger;
            this.SchedulerFactory = schedulerFactory;
            this.JobScheduleConfig = jobScheduleConfig;
            this.NodeConfig = nodeConfig;
            this.Options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.Scheduler = await this.SchedulerFactory.GetScheduler();
            await this.Scheduler.Start();
            JobScheduleTask scheduleTask = new JobScheduleTask(this.Logger, this.Scheduler);
            await scheduleTask.ScheduleAsync(this.JobScheduleConfig, this.NodeConfig);
            int parentProcessId = int.Parse(this.Options.parentProcessId);
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    using var process = Process.GetProcessById(parentProcessId);
                }
                catch (ArgumentException)
                {
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.ToString());
                }

            }
        }
    }
}
