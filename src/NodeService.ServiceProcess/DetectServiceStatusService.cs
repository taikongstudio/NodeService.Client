

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
                    if (!Path.IsPathFullyQualified(recoveryContext.InstallDirectory))
                    {
                        var path = Path.Combine(AppContext.BaseDirectory, recoveryContext.InstallDirectory);
                        recoveryContext.InstallDirectory = Path.GetFullPath(path);
                    }
                    
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
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RecoveryServiceAsync();
                await Task.Delay(TimeSpan.FromSeconds(30));
            }


        }

        private async ValueTask RecoveryServiceAsync(CancellationToken stoppingToken = default)
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


