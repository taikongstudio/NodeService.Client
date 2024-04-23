

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

        private Type ResolveTaskType(string taskTypeFullName)
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
                message = $"parameters is null";
                return;
            }
            try
            {
                await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Started, "Started");
                if (_taskExecutionContext.Parameters == null)
                {
                    message = $"parameters is null";
                    result = false;
                    return;
                }
                var jobTypeDescConfig = _taskExecutionContext.Parameters.TaskScheduleConfig.JobTypeDesc;
                if (jobTypeDescConfig == null)
                {
                    message = $"Could not found task type description config";
                    result = false;
                    return;
                }

                var serviceType = ResolveTaskType(jobTypeDescConfig.FullName);
                using var scope = this._serviceProvider.CreateAsyncScope();
                var task = scope.ServiceProvider.GetService(serviceType) as TaskBase;
                if (task == null)
                {
                    result = false;
                }
                else
                {
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Running, "Running");
                    task.SetJobScheduleConfig(_taskExecutionContext.Parameters.TaskScheduleConfig);
                    await task.ExecuteAsync(_taskExecutionContext.CancellationToken);
                    result = true;
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken == _taskExecutionContext.CancellationToken)
                {
                    message = ex.ToString();
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
                if (_taskExecutionContext.CancelledManually)
                {
                    message = "Cancelled manually";
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Cancelled, message);
                }
                else if (!result)
                {
                    message = "Failed";
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Failed, message);
                }
                else
                {
                    message = "Finished";
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Finished, message);
                }
            }
        }
    }
}
