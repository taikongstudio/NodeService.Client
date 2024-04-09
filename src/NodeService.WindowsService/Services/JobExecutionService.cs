using Microsoft.Extensions.DependencyInjection;
using NodeService.Infrastructure.Interfaces;
using NodeService.WindowsService.Collections;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static NodeService.WindowsService.Services.NodeClientService;

namespace NodeService.WindowsService.Services
{
    public class JobExecutionService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobExecutionService> _logger;
        private readonly JobExecutionContext _jobExecutionContext;

        public JobExecutionService(
            IServiceProvider serviceProvider,
            ILogger<JobExecutionService> logger,
            JobExecutionContext jobExecutionContext
            )
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _jobExecutionContext = jobExecutionContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_jobExecutionContext == null)
            {
                return;
            }
            bool result = true;
            string errorMessage = string.Empty;
            try
            {
                await _jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Started, "Started");
                if (_jobExecutionContext.Parameters == null)
                {
                    errorMessage = $"parameters is null";
                    result = false;
                    return;
                }
                var jobTypeDescConfig = _jobExecutionContext.Parameters.JobScheduleConfig.JobTypeDesc;
                if (jobTypeDescConfig == null)
                {
                    errorMessage = $"Could not found job type description config";
                    result = false;
                    return;
                }

                var serviceType = typeof(Job).Assembly.GetType(jobTypeDescConfig.FullName);
                using var scope = this._serviceProvider.CreateAsyncScope();
                var job = scope.ServiceProvider.GetService(serviceType) as Job;
                if (job == null)
                {
                    result = false;
                }
                else
                {
                    await _jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Running, "Running");
                    job.SetJobScheduleConfig(_jobExecutionContext.Parameters.JobScheduleConfig);
                    await job.ExecuteAsync(_jobExecutionContext.CancellationToken);
                    result = true;
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken == _jobExecutionContext.CancellationToken)
                {
                    errorMessage = ex.ToString();
                    return;
                }
                result = false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.ToString();
                result = false;
            }
            finally
            {
                if (_jobExecutionContext.CancelledManually)
                {
                    await _jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Cancelled, errorMessage);
                }
                else if (!result)
                {
                    await _jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Failed, errorMessage);
                }
                else
                {
                    await _jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Finished, errorMessage);
                }
            }
        }
    }
}
