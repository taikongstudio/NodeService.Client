using Confluent.Kafka;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI;
using NodeService.Infrastructure.Interfaces;
using NodeService.Infrastructure.MessageQueues;
using NodeService.Infrastructure.Models;
using Org.BouncyCastle.Ocsp;
using Quartz.Util;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace NodeService.WindowsService.Services
{
    public partial class NodeClientService
    {

        private readonly ConcurrentDictionary<string, JobContext> _jobExecutionContextDict;
        private  Channel<JobExecutionReport>  _reportChannel;
        private ActionBlock<LogMessageEntry> _logEntryActionBlock;
        private IAsyncQueue<JobExecutionParameters> _jobExecutionParamtersQueue;

        private void InitReportActionBlock()
        {
            _logEntryActionBlock = new ActionBlock<LogMessageEntry>(WriteLogMessageEntry, new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1,
            });
            _reportChannel = Channel.CreateUnbounded<JobExecutionReport>();
        }

        private async Task WriteLogMessageEntry(LogMessageEntry entry)
        {
            if (entry.JobId == null)
            {
                return;
            }
            var jobInstance = FindJobInstance(entry.JobId);
            if (jobInstance == null)
            {
                return;
            }
            await jobInstance.UpdateStatusAsync(jobInstance.Status, new JobExecutionLogEntry()
            {
                DateTime = Timestamp.FromDateTime(entry.DateTime),
                Type = (JobExecutionLogEntry.Types.JobExecutionLogEntryType)entry.Type,
                Value = entry.Value,
            });
        }


        private async Task ProcessJobExecutionRequestEventAsync(JobExecutionEventRequest request)
        {
            switch (request.Parameters["RequestType"])
            {
                case "Trigger":
                    await ProcessJobTriggerEventAsync(request);
                    break;
                case "Reinvoke":
                    await ProcessJobCancelRequestEventAsync(request);
                    await ProcessJobTriggerEventAsync(request);
                    break;
                case "Cancel":
                    await ProcessJobCancelRequestEventAsync(request);
                    break;
                default:
                    break;
            }

        }

        private async Task ProcessJobCancelRequestEventAsync(JobExecutionEventRequest request)
        {
            var rsp = new JobExecutionEventResponse()
            {
                ErrorCode = 0,
                Message = string.Empty,
                NodeName = request.NodeName,
                RequestId = request.RequestId
            };
            foreach (var kv in request.Parameters)
            {
                rsp.Parameters.Add(kv.Key, kv.Value);
            }
            string id = request.Parameters[nameof(JobExecutionInstanceModel.Id)];
            if (_jobExecutionContextDict.TryRemove(id, out var jobInstance))
            {
                jobInstance.Cancel();
                await jobInstance.UpdateStatusAsync(JobExecutionStatus.Cancelled, "Cancelled");

            }
            else
            {
                rsp.ErrorCode = -1;
                rsp.Message = $"invalid job instance id:{id}";
            }
            await this._nodeServiceClient.SendJobExecutionEventResponseAsync(rsp);
        }

        private async Task ProcessJobTriggerEventAsync(JobExecutionEventRequest request)
        {
            var rsp = new JobExecutionEventResponse()
            {
                ErrorCode = 0,
                Message = string.Empty,
                NodeName = request.NodeName,
                RequestId = request.RequestId
            };
            foreach (var kv in request.Parameters)
            {
                rsp.Parameters.Add(kv.Key, kv.Value);
            }
            var jobExecutionContext = _jobExecutionContextDict.GetOrAdd(request.Parameters[nameof(JobExecutionInstanceModel.Id)],
                            (key) => new JobContext(_serviceProvider,
                            request.NodeName,
                            JobExecutionParameters.BuildParameters(request.Parameters),
                            _reportChannel));
            await jobExecutionContext.UpdateStatusAsync(JobExecutionReport.Types.JobExecutionStatus.Pendding, "Pendding");
            await this._jobExecutionParamtersQueue.EnqueueAsync(jobExecutionContext.Parameters);
            await this._nodeServiceClient.SendJobExecutionEventResponseAsync(rsp);
        }

        private bool StartServiceHost(CancellationToken cancellationToken = default)
        {
            try
            {
                var task = new Task(async () =>
                {
                    await RunServiceHostImpl(cancellationToken);
                }, cancellationToken, TaskCreationOptions.LongRunning);
                task.Start();
                return true;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
            return false;
        }

        private JobContext? FindJobInstance(string jobInstanceId)
        {
            if (this._jobExecutionContextDict.TryGetValue(jobInstanceId, out var value))
            {
                return value;
            }
            return null;
        }

        private async Task RunServiceHostImpl(CancellationToken cancellationToken = default)
        {
            try
            {
                HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
                builder.Services.AddSingleton<IAsyncQueue<JobExecutionParameters>>(_jobExecutionParamtersQueue);
                builder.Services.AddHostedService<JobHostService>();
                builder.Services.AddScoped<ExecuteBatchScriptJob>();
                builder.Services.AddScoped<ExecutePythonCodeJob>();
                builder.Services.AddScoped<ShouHuUploadJob>();
                builder.Services.AddScoped<FtpUploadJob>();
                builder.Services.AddScoped<LogUploadJob>();
                builder.Services.AddScoped<FtpDownloadJob>();
                builder.Services.AddScoped(sp => new HttpClient
                {
                    BaseAddress = new Uri(builder.Configuration.GetValue<string>("ServerConfig:HttpAddress"))
                });
                builder.Services.AddScoped<ApiService>();
                builder.Services.AddSingleton<ActionBlock<LogMessageEntry>>(this._logEntryActionBlock);
                builder.Services.AddSingleton<Func<string, JobContext?>>(FindJobInstance);
                builder.Services.AddScoped<ILogger>(sp =>
                sp.GetService<ILoggerFactory>().CreateLogger("JobLogger")
                ); ;

                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.AddJobServiceLogger();
                using var app = builder.Build();

                await app.RunAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

    }
}
