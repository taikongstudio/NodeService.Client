using NodeService.ServiceHost.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            try
            {
                if (_serviceOptions == null || !int.TryParse(_serviceOptions.pid, out var pid))
                {
                    _logger.LogInformation("no parent process");
                    Console.Out.Flush();
                    Environment.Exit(0);
                    return;
                }
                var parentProcess = Process.GetProcessById(pid);
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
