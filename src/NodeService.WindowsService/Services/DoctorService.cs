using NodeService.ServiceHost.Models;
using System.ServiceProcess;

namespace NodeService.ServiceHost.Services
{
    public class DoctorService : BackgroundService
    {
        private readonly ILogger<DoctorService> _logger;
        private readonly ServiceOptions _serviceOptions;

        public DoctorService(
            ServiceOptions serviceOptions,
            ILogger<DoctorService> logger)
        {
            _logger = logger;
            _serviceOptions = serviceOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await StartAndCheckServiceStatusAsync(stoppingToken))
                {
                    Environment.Exit(0);
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }

        }

        private async Task<bool> StartAndCheckServiceStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string serviceName = $"NodeService.{_serviceOptions.mode}";
                using ServiceController serviceController = new ServiceController(serviceName);
                if (serviceController.Status != ServiceControllerStatus.Running)
                {
                    serviceController.Start();
                }
                else
                {
                    return true;
                }
                _logger.LogInformation($"等待服务\"{serviceName}\"运行");
                Stopwatch stopwatch = Stopwatch.StartNew();
                int waitCount = 1;
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(180));
                stopwatch.Stop();
                _logger.LogInformation($"服务\"{serviceName}\"已运行，等待：{stopwatch.Elapsed}");
                do
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    stopwatch.Restart();
                    serviceController.Refresh();
                    if (serviceController.Status != ServiceControllerStatus.Running)
                    {
                        _logger.LogInformation($"\"{serviceName}\"状态：{serviceController.Status}，尝试启动服务");
                        serviceController.Start();
                    }
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    stopwatch.Stop();
                    _logger.LogInformation($"持续观察服务\"{serviceName}\"状态，第{waitCount}次，等待：{stopwatch.Elapsed}");
                    stopwatch.Reset();
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    waitCount++;
                } while (waitCount < 12);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }
    }
}
