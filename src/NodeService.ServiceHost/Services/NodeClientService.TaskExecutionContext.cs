using NodeService.ServiceHost.Services;
using System.Reflection.Metadata;

namespace NodeService.WindowsService.Services
{
    public partial class NodeClientService
    {

        private IAsyncQueue<JobExecutionReport>  _reportQueue;
        private IAsyncQueue<TaskExecutionContext> _taskExecutionContextQueue;
        private readonly TaskExecutionContextDictionary _taskExecutionContextDictionary;

        private async Task ProcessJobExecutionEventRequest(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            var req = subscribeEvent.JobExecutionEventRequest;
            try
            {
                await ProcessTaskExecutionRequestEventAsync(req);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task ProcessTaskExecutionRequestEventAsync(JobExecutionEventRequest request)
        {
            switch (request.Parameters["RequestType"])
            {
                case "Trigger":
                    await ProcessTaskTriggerEventAsync(request);
                    break;
                case "Reinvoke":
                    await ProcessTaskCancelRequestEventAsync(request);
                    await ProcessTaskTriggerEventAsync(request);
                    break;
                case "Cancel":
                    await ProcessTaskCancelRequestEventAsync(request);
                    break;
                default:
                    break;
            }

        }

        private async Task ProcessTaskCancelRequestEventAsync(JobExecutionEventRequest request)
        {
            var rsp = new JobExecutionEventResponse()
            {
                ErrorCode = 0,
                Message = string.Empty,
                RequestId = request.RequestId
            };
            string taskId = request.Parameters[nameof(JobExecutionInstanceModel.Id)];
            if (_taskExecutionContextDictionary.TryRemove(taskId, out var jobExecutionContext))
            {
                await jobExecutionContext.DisposeAsync();
                rsp.Message = $"task {taskId} cancelled";
            }
            else
            {
                rsp.ErrorCode = -1;
                rsp.Message = $"invalid task instance id:{taskId}";
                var report = new JobExecutionReport();
                report.Status = JobExecutionStatus.Cancelled;
                report.Id = taskId;
                report.Message = "Cancelled";
                await this._reportQueue.EnqueueAsync(report);
            }

            await this._nodeServiceClient.SendJobExecutionEventResponseAsync(rsp, _headers);
        }

        private async Task ProcessTaskTriggerEventAsync(JobExecutionEventRequest request)
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
            var taskExecutionContext = _taskExecutionContextDictionary.GetOrAdd(
                request.Parameters[nameof(JobExecutionInstanceModel.Id)],
                BuildTaskExecutionContext(request.Parameters));
            await this._taskExecutionContextQueue.EnqueueAsync(taskExecutionContext);
            rsp.Message = $"{Dns.GetHostName()} recieved task";
            await this._nodeServiceClient.SendJobExecutionEventResponseAsync(rsp, _headers);
        }

        private TaskExecutionContext BuildTaskExecutionContext(IDictionary<string,string> parameters)
        {
            return new TaskExecutionContext(_serviceProvider.GetService<ILogger<TaskExecutionContext>>(),
                                        TaskCreationParameters.Build(parameters),
                                        _reportQueue,
                                        _taskExecutionContextDictionary);
        }
    }
}
