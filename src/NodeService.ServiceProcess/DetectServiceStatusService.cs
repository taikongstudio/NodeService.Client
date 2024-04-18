

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace NodeService.ServiceProcess
{
    public class DetectServiceStatusService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DetectServiceStatusService> _logger;
        private ServiceProcessConfiguration _appConfig = new ServiceProcessConfiguration();
        private DetectServiceStatusServiceContext _context;
        private int _installFailedCount;
        private ConcurrentDictionary<string, ApiService> _apiServices;

        public DetectServiceStatusService(
            IServiceProvider serviceProvider,
            ILogger<DetectServiceStatusService> logger,
            IOptionsMonitor<ServiceProcessConfiguration> optionsMonitor
            )
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _appConfig = optionsMonitor.CurrentValue;
            ApplyConfiguration(_appConfig);
            optionsMonitor.OnChange(OnConfigurationChanged);
            _apiServices = new ConcurrentDictionary<string, ApiService>();
        }

        private void OnConfigurationChanged(ServiceProcessConfiguration configuration, string name)
        {
            ApplyConfiguration(configuration);
        }

        private void ApplyConfiguration(ServiceProcessConfiguration configuration)
        {
            try
            {
                _appConfig = configuration;
                var context = new DetectServiceStatusServiceContext();

                if (_context!=null)
                {
                    foreach (var item in _context.RecoveryContexts)
                    {
                        item.Value.Dispose();
                    }
                    _context.RecoveryContexts.Clear();
                }

                foreach (var recoveryContext in configuration.Contexts)
                {
                    context.RecoveryContexts.Add(
                        recoveryContext.ServiceName,
                        new ServiceProcessRecovey(
                            _serviceProvider.GetService<ILogger<ServiceProcessRecovey>>(),
                            recoveryContext));
                }
                _context = context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RecoveryService();
                await Task.Delay(TimeSpan.FromSeconds(30));
            }


        }

        private async ValueTask RecoveryService(CancellationToken stoppingToken = default)
        {
            try
            {
                if (this._context == null)
                {
                    return;
                }
                if (_context.RecoveryContexts == null || !_context.RecoveryContexts.Any())
                {
                    return;
                }
                foreach (var kv in _context.RecoveryContexts)
                {
                    try
                    {
                        await kv.Value.ExecuteAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }


    }
}


