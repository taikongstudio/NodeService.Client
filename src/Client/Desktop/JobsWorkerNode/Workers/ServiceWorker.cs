using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNode.Workers
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
