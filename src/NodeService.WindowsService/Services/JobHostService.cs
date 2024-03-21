using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using NodeService.Infrastructure.Interfaces;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{

    public class JobHostService : BackgroundService

    {
        private readonly IAsyncQueue<JobExecutionParameters> _jobExecutionParametersQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<string, NodeClientService.JobContext?> _findJobExecutionContextFunc;

        public JobHostService(
            IServiceProvider serviceProvider,
            IAsyncQueue<JobExecutionParameters> queue,
            Func<string, NodeClientService.JobContext?> findJobExecutionContextFunc
            )
        {
            _jobExecutionParametersQueue = queue;
            _serviceProvider = serviceProvider;
            _findJobExecutionContextFunc = findJobExecutionContextFunc;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var parameters = await _jobExecutionParametersQueue.DeuqueAsync(stoppingToken);
                var jobExecutionContext = this._findJobExecutionContextFunc(parameters.Id);
                if (jobExecutionContext == null)
                {
                    continue;
                }
                var task = new Task(async () =>
                {

                    if (jobExecutionContext == null)
                    {
                        return;
                    }
                    bool result = true;
                    string errorMessage = string.Empty;
                    try
                    {
                        await jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Started, "Started");
                        if (parameters == null)
                        {
                            errorMessage = $"parameters is null";
                            result = false;
                            return;
                        }
                        var jobTypeDescConfig = parameters.JobScheduleConfig.JobTypeDesc;
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
                            job.JobScheduleConfig = parameters.JobScheduleConfig;
                            await jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Running, "Running");
                            using (job.Logger.BeginScope(jobExecutionContext.Parameters.Id))
                            {
                                await job.ExecuteAsync(jobExecutionContext.CancellationToken);
                            }
                            result = true;
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (ex.CancellationToken == jobExecutionContext.CancellationToken)
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
                        if (jobExecutionContext.StopManually)
                        {
                            await jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Cancelled, errorMessage);
                        }
                        else if (!result)
                        {
                            await jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Failed, errorMessage);
                        }
                        else
                        {
                            await jobExecutionContext.UpdateStatusAsync(JobExecutionStatus.Finished, errorMessage);
                        }

                    }


                }, jobExecutionContext.CancellationToken, TaskCreationOptions.LongRunning);
                task.Start();
            }
        }




    }
}
