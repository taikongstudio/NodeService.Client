using NodeService.ServiceHost.Models;

namespace NodeService.ServiceHost.Services
{
    public class ParentProcessMonitorService : BackgroundService
    {
        private readonly ILogger<ParentProcessMonitorService> _logger;
        private readonly ServiceOptions _serviceOptions;

        public ParentProcessMonitorService(
            ILogger<ParentProcessMonitorService> logger,
            ServiceOptions serviceOptions)
        {
            _logger = logger;
            _serviceOptions = serviceOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (Debugger.IsAttached)
            {
                return;

            }
            try
            {
                if (_serviceOptions == null || !int.TryParse(_serviceOptions.pid, out var pid))
                {
                    _logger.LogInformation("no parent process");
                    Console.Out.Flush();
                    Environment.Exit(0);
                    return;
                }
                using var parentProcess = Process.GetProcessById(pid);
                await parentProcess.WaitForExitAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                Environment.Exit(0);
            }

        }
    }
}
