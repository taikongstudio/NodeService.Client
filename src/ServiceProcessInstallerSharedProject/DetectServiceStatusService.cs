using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeService.Infrastructure;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceProcess;

public class DetectServiceStatusService : BackgroundService
{
    private readonly ILogger<DetectServiceStatusService> _logger;
    private AppConfig _appConfig = new AppConfig();
    private DetectServiceStatusServiceContext _context;

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
                        var installer = MyServiceProcessInstaller.Create(
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
                            return false;
                        }
                        var clientUpdateConfig = rsp.Result;
                        if (clientUpdateConfig == null)
                        {
                            return false;
                        }
                        var packageConfig = clientUpdateConfig.PackageConfig;
                        if (packageConfig == null)
                        {
                            return false;
                        }
                        installer.SetParameters(
                            new HttpPackageProvider(apiService, packageConfig),
                            new ServiceProcessInstallContext(packageUpdate.ServiceName,
                                packageUpdate.DisplayName,
                                packageUpdate.Description,
                                packageUpdate.InstallDirectory));

                        return await installer.RunAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
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
                await Task.Delay(TimeSpan.FromMinutes(Debugger.IsAttached ? 0 : 5), stoppingToken);
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

    private async Task RunRecovery(string serviceName, Func<string, Task<bool>> recoveryFunc)
    {
        try
        {
            await recoveryFunc.Invoke(serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
        }

    }
}
