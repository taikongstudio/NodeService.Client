using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.Daemon.Workers
{
    public class ServiceWorker : BackgroundService
    {
        private ILogger<ServiceWorker> _logger;

        public ServiceWorker(ILogger<ServiceWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var exitFileName = Path.Combine(AppContext.BaseDirectory, "exit.txt");
            var currentProcessExitFileName = Path.Combine(AppContext.BaseDirectory, Process.GetCurrentProcess().Id.ToString() + ".txt");
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
                    if (File.Exists(currentProcessExitFileName))
                    {
                        File.Delete(currentProcessExitFileName);
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
