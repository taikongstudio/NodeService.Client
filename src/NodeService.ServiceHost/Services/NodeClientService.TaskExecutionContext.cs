using NodeService.Infrastructure.Concurrent;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {

        private IAsyncQueue<TaskExecutionReport> _taskExecutionReportQueue;
        private IAsyncQueue<TaskExecutionContext> _taskExecutionContextQueue;
        private readonly TaskExecutionContextDictionary _taskExecutionContextDictionary;

        private async Task ProcessTaskExecutionEventRequest(
            NodeServiceClient nodeServiceClient,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            var req = subscribeEvent.TaskExecutionEventRequest;
            try
            {
                switch (req.Parameters["RequestType"])
                {
                    case "Trigger":
                        await ProcessTaskTriggerEventAsync(
                            nodeServiceClient,
                            req,
                            cancellationToken);
                        break;
                    case "Reinvoke":
                        await ProcessTaskCancelRequestEventAsync(
                            nodeServiceClient,
                            req,
                            cancellationToken);
                        await ProcessTaskTriggerEventAsync(
                            nodeServiceClient,
                            req,
                            cancellationToken);
                        break;
                    case "Cancel":
                        await ProcessTaskCancelRequestEventAsync(
                            nodeServiceClient,
                            req,
                            cancellationToken);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }


        private async Task ProcessTaskCancelRequestEventAsync(
            NodeServiceClient nodeServiceClient,
            TaskExecutionEventRequest request,
            CancellationToken cancellationToken = default)
        {
            var rsp = new TaskExecutionEventResponse()
            {
                ErrorCode = 0,
                Message = string.Empty,
                RequestId = request.RequestId
            };
            string taskId = request.Parameters[nameof(TaskExecutionInstanceModel.Id)];
            if (_taskExecutionContextDictionary.TryRemove(taskId, out var taskExecutionContext))
            {
                await taskExecutionContext.CancelAsync();
                rsp.Message = $"task {taskId} cancelled";
            }
            else
            {
                rsp.ErrorCode = -1;
                rsp.Message = $"invalid task instance id:{taskId}";
                var report = new TaskExecutionReport
                {
                    Status = TaskExecutionStatus.Cancelled,
                    Id = taskId,
                    Message = "Cancelled"
                };
                await this._taskExecutionReportQueue.EnqueueAsync(report);
            }

            await nodeServiceClient.SendTaskExecutionEventResponseAsync(
                rsp,
                _headers,
                cancellationToken: cancellationToken);
        }

        private async Task ProcessTaskTriggerEventAsync(
            NodeServiceClient nodeServiceClient,
            TaskExecutionEventRequest request,
            CancellationToken cancellationToken = default)
        {
            var rsp = new TaskExecutionEventResponse()
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
                request.Parameters[nameof(TaskExecutionInstanceModel.Id)],
                BuildTaskExecutionContext(request.Parameters));
            await this._taskExecutionContextQueue.EnqueueAsync(taskExecutionContext);
            rsp.Message = $"{Dns.GetHostName()} recieved task";
            await nodeServiceClient.SendTaskExecutionEventResponseAsync(
                rsp,
                _headers,
                cancellationToken: cancellationToken);
        }

        private TaskExecutionContext BuildTaskExecutionContext(IDictionary<string, string> parameters)
        {
            return new TaskExecutionContext(_serviceProvider.GetService<ILogger<TaskExecutionContext>>(),
                                        TaskCreationParameters.Build(parameters),
                                        _taskExecutionReportQueue,
                                        _taskExecutionContextDictionary);
        }
    }
}
