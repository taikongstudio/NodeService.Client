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
using System.Runtime.CompilerServices;
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

        public UpdatePackageService(ILogger<UpdatePackageService> logger, IOptionsMonitor<UpdateConfig> config)
        {
            _logger = logger;
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
        }

        const string SERVICENAME = "NodeService.WindowsService";

        private async Task TryDeleteDaemonServiceAsync()
        {
            try
            {
                const string DaemonServiceDirectory = "C:\\shouhu\\DaemonService";
                WriteExitTxtFile(DaemonServiceDirectory);
                await Task.Delay(TimeSpan.FromSeconds(30));
                await ServiceHelper.UninstallAsync("JobsWorkerDaemonServiceWindowsService");
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
                using var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri(_currentConfig.HttpAddress),
                    Timeout = TimeSpan.FromSeconds(300)
                };
                ApiService apiService = new ApiService(httpClient);
                var apiResult = await apiService.QueryClientUpdateAsync(stoppingToken);
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


                    using var stream = new MemoryStream();
                    await apiService.DownloadPackageAsync(clientUpdateConfig.PackageConfig, stream, stoppingToken);
                    _logger.LogInformation($"Download {clientUpdateConfig}");
                    await apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
                    {
                        ClientUpdateConfigId = clientUpdateConfig.Id,
                        NodeName = Dns.GetHostName(),
                        CategoryName = "DownloadSuccess"
                    }, stoppingToken);
                    stream.Position = 0;
                    if (!IsZipArchive(stream))
                    {
                        return;
                    }
                    stream.Position = 0;
                    _logger.LogInformation($"Start Uninstall");
                    if (ServiceHelper.TryGetServiceState(SERVICENAME, out var serviceState)
                        && serviceState == ServiceHelper.ServiceState.Running
                        &&
                            Directory.Exists(_currentConfig.InstallDirectory))
                    {
                        WriteExitTxtFile(_currentConfig.InstallDirectory);
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                    bool uninstalledResult = await ServiceHelper.UninstallAsync(SERVICENAME);
                    _logger.LogInformation($"End Uninstall");
                    _logger.LogInformation($"Uninstall result:{uninstalledResult}");
                    if (uninstalledResult == true)
                    {
                        await apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
                        {
                            ClientUpdateConfigId = clientUpdateConfig.Id,
                            NodeName = Dns.GetHostName(),
                            CategoryName = "UninstallSuccess"
                        }, stoppingToken);
                    }

                    _logger.LogInformation($"Start CleanupInstallDirectory");
                    CleanupInstallDirectory();
                    _logger.LogInformation($"End CleanupInstallDirectory");

                    _logger.LogInformation($"Start Extract");
                    ZipFile.ExtractToDirectory(stream, destDirectory, true);
                    _logger.LogInformation($"End Extract");

                    await apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
                    {
                        ClientUpdateConfigId = clientUpdateConfig.Id,
                        NodeName = Dns.GetHostName(),
                        CategoryName = "ExtractSuccess"
                    }, stoppingToken);

                    File.WriteAllText(updateConfigFilePath, JsonSerializer.Serialize(clientUpdateConfig));
                    if (clientUpdateConfig.PackageConfig.EntryPoint == null)
                    {
                        _logger.LogError("invalid entry point");
                        return;
                    }
                    var entryPoint = Path.Combine(destDirectory, clientUpdateConfig.PackageConfig.EntryPoint);
                    if (!File.Exists(entryPoint))
                    {
                        return;
                    }

                    _logger.LogInformation($"Start Install");
                    ServiceHelper.Install(SERVICENAME,
                       SERVICENAME,
                        entryPoint,
                        clientUpdateConfig.Description ?? string.Empty,
                         ServiceStartType.AutoStart,
                          ServiceAccount.LocalSystem
                        );
                    _logger.LogInformation($"End Install");

                    _logger.LogInformation($"Start StartService");
                    bool startResult = await ServiceHelper.StartServiceAsync(SERVICENAME,
                        clientUpdateConfig.PackageConfig.Arguments == null ? [entryPoint] :
                      [string.Join(" ", entryPoint, clientUpdateConfig.PackageConfig.Arguments)]);
                    _logger.LogInformation($"End StartService");
                    _logger.LogInformation($"StartService:{startResult}");

                    if (startResult)
                    {
                        await apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
                        {
                            ClientUpdateConfigId = clientUpdateConfig.Id,
                            NodeName = Dns.GetHostName(),
                            CategoryName = "StartSuccess"
                        }, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    await apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
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
                    _logger.LogInformation("Same config");
                    if (ServiceHelper.TryGetServiceState(SERVICENAME, out var state))
                    {
                        _logger.LogInformation($"Service status:{state}");
                        if (state == ServiceHelper.ServiceState.Running)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            return false;
        }

        private void CleanupInstallDirectory()
        {
            try
            {
                var installDirectory = new DirectoryInfo(_currentConfig.InstallDirectory);
                foreach (var item in installDirectory.GetFileSystemInfos())
                {
                    if (Directory.Exists(item.FullName))
                    {
                        Directory.Delete(item.FullName, true);
                    }
                    else
                    {
                        item.Delete();
                    }
                }

            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }

        }

        private bool IsZipArchive(Stream stream)
        {
            try
            {
                using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.ToString());
            }
            finally
            {

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
