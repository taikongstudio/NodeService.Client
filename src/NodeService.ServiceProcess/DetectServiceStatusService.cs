

namespace NodeService.ServiceProcess
{
    public class DetectServiceStatusService : BackgroundService
    {
        private readonly ILogger<DetectServiceStatusService> _logger;
        private AppConfig _appConfig = new AppConfig();
        private DetectServiceStatusServiceContext _context;
        private int _installFailedCount;

        public DetectServiceStatusService(
            IServiceProvider serviceProvider,
            ILogger<DetectServiceStatusService> logger,
            IOptionsMonitor<AppConfig> optionsMonitor
            )
        {
            _logger = logger;
            _appConfig = optionsMonitor.CurrentValue;
            ApplyAppConfig(_appConfig);
            optionsMonitor.OnChange(OnPackageUpdateConfigChanged);
        }

        private void OnPackageUpdateConfigChanged(AppConfig appConfig, string name)
        {
            ApplyAppConfig(appConfig);
        }

        private void ApplyAppConfig(AppConfig appConfig)
        {
            try
            {
                _appConfig = appConfig;
                var context = new DetectServiceStatusServiceContext();
                foreach (var packageUpdate in appConfig.PackageUpdates)
                {
                    context.ServiceRecoveries.Add(packageUpdate.ServiceName, async (serviceName) =>
                    {
                        try
                        {
                            var filePath = Path.Combine(packageUpdate.InstallDirectory, packageUpdate.ServiceName + ".exe");
                            var installer = CommonServiceProcessInstaller.Create(
                                serviceName,
                                packageUpdate.DisplayName,
                                packageUpdate.Description,
                                filePath);
                            var apiService = new ApiService(new HttpClient()
                            {
                                BaseAddress = new Uri(packageUpdate.HttpAddress)
                            });
                            var rsp = await apiService.QueryClientUpdateAsync(packageUpdate.ServiceName);
                            if (rsp.ErrorCode != 0)
                            {
                                _logger.LogError(rsp.Message);
                                return false;
                            }
                            var clientUpdateConfig = rsp.Result;
                            if (clientUpdateConfig == null)
                            {
                                _logger.LogError("Could not find update");
                                return false;
                            }
                            var packageConfig = clientUpdateConfig.PackageConfig;
                            if (packageConfig == null)
                            {
                                _logger.LogError("Could not find package");
                                return false;
                            }
                            _logger.LogInformation(clientUpdateConfig.ToJsonString<ClientUpdateConfigModel>());
                            installer.SetParameters(
                                new HttpPackageProvider(apiService, packageConfig),
                                new ServiceProcessInstallContext(packageUpdate.ServiceName,
                                    packageUpdate.DisplayName,
                                    packageUpdate.Description,
                                    packageUpdate.InstallDirectory));

                            installer.ProgressChanged += Installer_ProgressChanged;
                            installer.Failed += Installer_Failed;
                            installer.Completed += Installer_Completed;

                            return await installer.RunAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.Message);
                        }
                        return false;

                    });
                }
                _context = context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private void Installer_Completed(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogInformation(e.Progress.Message);
        }

        private void Installer_Failed(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogError(e.Progress.Message);
        }

        private void Installer_ProgressChanged(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogInformation(e.Progress.Message);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await DetectServiceStatusAsync();
                await Task.Delay(TimeSpan.FromSeconds(30));
            }


        }

        private async ValueTask DetectServiceStatusAsync(CancellationToken stoppingToken = default)
        {
            foreach (var kv in _context.ServiceRecoveries)
            {
                await DetectServiceAsync(kv.Key, kv.Value, stoppingToken);
            }
        }

        private async Task DetectServiceAsync(
            string serviceName,
            Func<string, Task<bool>> recoveryFunc,
            CancellationToken stoppingToken = default)
        {
            try
            {
                using ServiceController serviceController = new ServiceController(serviceName);
                if (serviceController.Status == ServiceControllerStatus.Stopped)
                {
                    await Delay(stoppingToken);
                }
                serviceController.Refresh();
                if (serviceController.Status == ServiceControllerStatus.Stopped)
                {
                    await RunRecovery(serviceName, recoveryFunc);
                }
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is Win32Exception win32Exception && win32Exception.NativeErrorCode == 1060)
                {
                    _logger.LogInformation(ex.Message);
                    await RunRecovery(serviceName, recoveryFunc);
                }
                else
                {
                    _logger.LogError(ex.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task Delay(CancellationToken stoppingToken)
        {
            try
            {
                if (_installFailedCount > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                else
                {
                    var timeSpan =
                        Debugger.IsAttached ?
                        TimeSpan.Zero :
                        TimeSpan.FromSeconds(Random.Shared.Next(1, 3000));
                    await Task.Delay(timeSpan, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private async Task RunRecovery(string serviceName, Func<string, Task<bool>> recoveryFunc)
        {
            try
            {
                if (await recoveryFunc.Invoke(serviceName))
                {
                    _installFailedCount = 0;
                }
                else
                {
                    _installFailedCount++;
                }
      
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                _installFailedCount++;
            }

        }
    }
}


