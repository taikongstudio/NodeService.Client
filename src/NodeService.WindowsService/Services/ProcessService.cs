using NLog;
using NodeService.WindowsService;
using System.Diagnostics;

namespace NodeService.WindowsService.Services
{
    public class ProcessService : Microsoft.Extensions.Hosting.BackgroundService
    {
        private ILogger<ProcessService> _logger;

        public ProcessService(ILogger<ProcessService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var exitFileName = Path.Combine(AppContext.BaseDirectory, "exit.txt");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000);
                    if (File.Exists(exitFileName))
                    {
                        File.Delete(exitFileName);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    break;
                }

            }
            Environment.Exit(0);
        }

    }
}
