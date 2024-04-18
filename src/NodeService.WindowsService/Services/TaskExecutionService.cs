namespace NodeService.WindowsService.Services
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_taskExecutionContext == null)
            {
                return;
            }
            bool result = true;
            string errorMessage = string.Empty;
            try
            {
                await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Started, "Started");
                if (_taskExecutionContext.Parameters == null)
                {
                    errorMessage = $"parameters is null";
                    result = false;
                    return;
                }
                var jobTypeDescConfig = _taskExecutionContext.Parameters.TaskScheduleConfig.JobTypeDesc;
                if (jobTypeDescConfig == null)
                {
                    errorMessage = $"Could not found job type description config";
                    result = false;
                    return;
                }

                var serviceType = typeof(TaskBase).Assembly.GetType(jobTypeDescConfig.FullName);
                using var scope = this._serviceProvider.CreateAsyncScope();
                var job = scope.ServiceProvider.GetService(serviceType) as TaskBase;
                if (job == null)
                {
                    result = false;
                }
                else
                {
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Running, "Running");
                    job.SetJobScheduleConfig(_taskExecutionContext.Parameters.TaskScheduleConfig);
                    await job.ExecuteAsync(_taskExecutionContext.CancellationToken);
                    result = true;
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken == _taskExecutionContext.CancellationToken)
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
                if (_taskExecutionContext.CancelledManually)
                {
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Cancelled, errorMessage);
                }
                else if (!result)
                {
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Failed, errorMessage);
                }
                else
                {
                    await _taskExecutionContext.UpdateStatusAsync(JobExecutionStatus.Finished, errorMessage);
                }
            }
        }
    }
}
