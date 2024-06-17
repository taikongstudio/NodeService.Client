

using NodeService.ServiceHost.Tasks;
using Type = System.Type;

namespace NodeService.ServiceHost.Services
{
    public class TaskExecutionService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskExecutionService> _logger;
        private readonly TaskExecutionContext _taskExecutionContext;

        public TaskExecutionService(
            IServiceProvider serviceProvider,
            ILogger<TaskExecutionService> logger,
            TaskExecutionContext jobExecutionContext
            )
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _taskExecutionContext = jobExecutionContext;
        }

        private Type? ResolveTaskType(string taskTypeFullName)
        {
            if (taskTypeFullName.StartsWith("NodeService.WindowsService.Services"))
            {
                taskTypeFullName = $"NodeService.ServiceHost.Tasks.{taskTypeFullName.Substring(taskTypeFullName.LastIndexOf('.') + 1)}";
                return Type.GetType(taskTypeFullName);
            }
            return Type.GetType(taskTypeFullName);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool result = true;
            string message = string.Empty;
            if (_taskExecutionContext == null)
            {
                message = $"Task execution context is null";
                return;
            }
            try
            {
                await _taskExecutionContext.UpdateStatusAsync(TaskExecutionStatus.Started, "Started");
                if (_taskExecutionContext.Parameters == null)
                {
                    message = $"Parameters is null";
                    result = false;
                    return;
                }
                var taskTypeDesc = _taskExecutionContext.Parameters.TaskTypeDefinition.TaskTypeDesc;
                if (taskTypeDesc == null)
                {
                    message = $"Could not found task type description config";
                    result = false;
                    return;
                }

                var serviceType = ResolveTaskType(taskTypeDesc.FullName);
                if (serviceType == null)
                {
                    message = $"Could not found task type {taskTypeDesc.FullName}";
                    result = false;
                    return;
                }
                using var scope = this._serviceProvider.CreateAsyncScope();
                var task = scope.ServiceProvider.GetService(serviceType) as TaskBase;
                if (task == null)
                {
                    result = false;
                }
                else
                {
                    await _taskExecutionContext.UpdateStatusAsync(TaskExecutionStatus.Running, "Running");
                    task.SetTaskDefinition(_taskExecutionContext.Parameters.TaskTypeDefinition);
                    task.SetEnvironmentVariables(_taskExecutionContext.Parameters.EnvironmentVariables);
                    await task.ExecuteAsync(_taskExecutionContext.CancellationToken);
                    result = true;
                }
            }
            catch (OperationCanceledException ex)
            {
                message = ex.ToString();
                if (ex.CancellationToken == _taskExecutionContext.CancellationToken)
                {
                    return;
                }
                result = false;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                result = false;
            }
            finally
            {
                await _taskExecutionContext.DisposeAsync();
                if (_taskExecutionContext.CancelledManually)
                {
                    message = "Cancelled manually";
                    await _taskExecutionContext.UpdateStatusAsync(TaskExecutionStatus.Cancelled, message);
                }
                else if (!result)
                {
                    await _taskExecutionContext.UpdateStatusAsync(TaskExecutionStatus.Failed, message);
                }
                else
                {
                    message = "Finished";
                    await _taskExecutionContext.UpdateStatusAsync(TaskExecutionStatus.Finished, message);
                }
            }
        }
    }
}
