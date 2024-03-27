using Confluent.Kafka;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI;
using NodeService.Infrastructure.Interfaces;
using NodeService.Infrastructure.Models;
using NodeService.WindowsService.Collections;
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

        private IAsyncQueue<JobExecutionReport>  _reportQueue;
        private IAsyncQueue<JobExecutionContext> _jobExecutionContextQueue;
        private readonly JobContextDictionary _jobContextDictionary;


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
                RequestId = request.RequestId
            };
            foreach (var kv in request.Parameters)
            {
                rsp.Parameters.Add(kv.Key, kv.Value);
            }
            string id = request.Parameters[nameof(JobExecutionInstanceModel.Id)];
            if (_jobContextDictionary.TryRemove(id, out var jobContext))
            {
                jobContext.Cancel();
                await jobContext.UpdateStatusAsync(JobExecutionStatus.Cancelled, "Cancelled");

            }
            else
            {
                rsp.ErrorCode = -1;
                rsp.Message = $"invalid job instance id:{id}";
                var report = new JobExecutionReport()
                {
                    CreatedDateTime = DateTime.Now
                };
                report.Status = JobExecutionStatus.Cancelled;
                foreach (var kv in request.Parameters)
                {
                    report.Properties.Add(kv.Key, kv.Value);
                }
                await this._reportQueue.EnqueueAsync(report);
            }

            await this._nodeServiceClient.SendJobExecutionEventResponseAsync(rsp, _headers);
        }

        private async Task ProcessJobTriggerEventAsync(JobExecutionEventRequest request)
        {
            var rsp = new JobExecutionEventResponse()
            {
                ErrorCode = 0,
                Message = string.Empty,
                RequestId = request.RequestId
            };
            foreach (var kv in request.Parameters)
            {
                rsp.Parameters.Add(kv.Key, kv.Value);
            }
            var jobExecutionContext = _jobContextDictionary.GetOrAdd(request.Parameters[nameof(JobExecutionInstanceModel.Id)],
                            (key) => new JobExecutionContext(_serviceProvider.GetService<ILogger<JobExecutionContext>>(),
                            JobExecutionParameters.BuildParameters(request.Parameters),
                            _reportQueue,
                            _jobContextDictionary));
            await this._jobExecutionContextQueue.EnqueueAsync(jobExecutionContext);
            rsp.Message = $"{Dns.GetHostName()} recieved ";
            await this._nodeServiceClient.SendJobExecutionEventResponseAsync(rsp, _headers);
        }

    }
}
