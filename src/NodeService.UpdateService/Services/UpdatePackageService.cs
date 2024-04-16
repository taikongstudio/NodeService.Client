using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using NodeService.UpdateService;
using NodeService.UpdateService.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NodeService.UpdateService.Services
{
    public class UpdatePackageService : BackgroundService
    {
        private readonly ILogger _logger;
        private UpdateConfig _currentConfig;
        private IDisposable? _onChangeToken;
        private string _tempPath;
        private ApiService  _apiService;
        private string _clientUpdateConfigId;

        public UpdatePackageService(ILogger<UpdatePackageService> logger, IOptionsMonitor<UpdateConfig> config)
        {
            _clientUpdateConfigId = "NotInstalled";
            _logger = logger;
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(OnUpdateConfigChanged);
            InitApiService();
        }

        private void OnUpdateConfigChanged(UpdateConfig updateConfig)
        {
            DisposeApiService();
            InitApiService();
        }

        private void DisposeApiService()
        {
            if (this._apiService != null)
            {
                this._apiService.Dispose();
                this._apiService = null;
            }
        }

        private void InitApiService()
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(_currentConfig.HttpAddress),
                Timeout = TimeSpan.FromSeconds(300)
            };
            this._apiService = new ApiService(httpClient);
        }

        const string ServiceName = "NodeService.WindowsService";

        private async Task TryDeleteDaemonServiceAsync()
        {
            try
            {
                const string DaemonServiceDirectory = "C:\\shouhu\\DaemonService";
                WriteExitTxtFile(DaemonServiceDirectory);
                await Task.Delay(TimeSpan.FromSeconds(30));

                using var installer= ServiceProcessInstallerHelper.Create(
                    "JobsWorkerDaemonService",
                    null,
                    null,
                    null);

                installer.Uninstall(null);

                if (Directory.Exists(DaemonServiceDirectory))
                {
                    Directory.Delete(DaemonServiceDirectory, true);
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }
        }

        private static void WriteExitTxtFile(string directory)
        {
            string exitTxtFilePath = Path.Combine(directory, "exit.txt");
            File.WriteAllText(exitTxtFilePath, "");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteUpdateAsync(stoppingToken);
            }
        }

        private async Task ExecuteUpdateAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                await TryDeleteDaemonServiceAsync();
                var apiResult = await _apiService.QueryClientUpdateAsync(stoppingToken);
                if (apiResult.ErrorCode != 0)
                {
                    _logger.LogError(apiResult.Message);
                    return;
                }

                var clientUpdateConfig = apiResult.Result;
                if (clientUpdateConfig == null || clientUpdateConfig.PackageConfig == null)
                {
                    _logger.LogError("invalid client config");
                    return;
                }
                _logger.LogInformation($"Query config: {clientUpdateConfig.ToJsonString<ClientUpdateConfigModel>()}");
                var destDirectory = Path.Combine(_currentConfig.InstallDirectory);
                var updateConfigFilePath = Path.Combine(_currentConfig.InstallDirectory, "UpdateConfig");
                try
                {
                    if (TryCompareConfig(clientUpdateConfig, updateConfigFilePath))
                    {
                        _logger.LogInformation("Same config,skip update");
                        return;
                    }
                    if (clientUpdateConfig.PackageConfig.EntryPoint == null)
                    {
                        _logger.LogError("invalid entry point");
                        return;
                    }
                    var entryPoint = Path.Combine(_currentConfig.InstallDirectory, clientUpdateConfig.PackageConfig.EntryPoint);
                    using var installer = WindowsServiceProcessInstaller.Create(ServiceName, ServiceName, string.Empty, entryPoint);
                    installer.Failed += Installer_Failed;
                    installer.ProgressChanged += Installer_ProgressChanged;
                    installer.Completed += Installer_Completed; ;
                    installer.SetParameters(_apiService, clientUpdateConfig, _currentConfig.InstallDirectory);

                    await installer.RunAsync();

                    File.WriteAllText(updateConfigFilePath, JsonSerializer.Serialize(clientUpdateConfig));


                }
                catch (Exception ex)
                {
                    await _apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
                    {
                        ClientUpdateConfigId = clientUpdateConfig.Id,
                        NodeName = Dns.GetHostName(),
                        CategoryName = ex.ToString()
                    }, stoppingToken);
                    _logger.LogError(ex.ToString());
                }
                finally
                {
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                    GC.WaitForPendingFinalizers();
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                await DelayAsync(stoppingToken);
            }
        }

        private void Installer_Completed(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogInformation(e.Progress.Message);
        }

        private async void Installer_ProgressChanged(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogInformation(e.Progress.Message);
            await _apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
            {
                ClientUpdateConfigId = _clientUpdateConfigId,
                NodeName = Dns.GetHostName(),
                CategoryName = e.Progress.Message
            });
        }

        private void Installer_Failed(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogError(e.Progress.Message);
        }

        private TimeSpan GetDurationMinutes()
        {
            if (Debugger.IsAttached)
            {
                return TimeSpan.FromSeconds(5);
            }
            if (_currentConfig == null)
            {
                return TimeSpan.FromMinutes(10);
            }
            return TimeSpan.FromMinutes(Math.Min(10, Math.Max(0, _currentConfig.DurationMinutes)));
        }

        private async ValueTask DelayAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                await Task.Delay(GetDurationMinutes(), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private bool TryCompareConfig(ClientUpdateConfigModel clientUpdateConfig, string updateConfigFilePath)
        {
            try
            {
                if (!File.Exists(updateConfigFilePath))
                {
                    return false;
                }
                var jsonText = File.ReadAllText(updateConfigFilePath);
                var installedConfig = JsonSerializer.Deserialize<ClientUpdateConfigModel>(jsonText);
                if (installedConfig == null)
                {
                    return false;
                }

                if (clientUpdateConfig.PackageConfig == null)
                {
                    return false;
                }

                if (installedConfig.PackageConfig == null)
                {
                    return false;
                }

                if (clientUpdateConfig.PackageConfig.Hash == installedConfig.PackageConfig.Hash)
                {
                    _clientUpdateConfigId = clientUpdateConfig.Id;
                    _logger.LogInformation("Same config");
                    using var serviceController = new ServiceController(ServiceName);
                    return serviceController.Status == ServiceControllerStatus.Running;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            return false;
        }

        public override void Dispose()
        {
            _onChangeToken?.Dispose();
            base.Dispose();
        }

    }
}
