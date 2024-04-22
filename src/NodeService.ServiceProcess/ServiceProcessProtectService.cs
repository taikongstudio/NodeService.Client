


using Microsoft.Win32.TaskScheduler;

namespace NodeService.ServiceProcess
{
    public class ServiceProcessProtectService : BackgroundService
    {
        private readonly ILogger<ServiceProcessProtectService> _logger;
        private readonly ServiceProcessProtectServiceOptions _options;

        public ServiceProcessProtectService(
            ILogger<ServiceProcessProtectService> logger,
            ServiceProcessProtectServiceOptions options)
        {
            _logger = logger;
            _options = options;
        }

        protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await System.Threading.Tasks.Task.CompletedTask;
        }

    }
}
