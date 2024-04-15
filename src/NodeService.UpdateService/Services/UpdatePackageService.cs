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
            _tempPath = Path.Combine(Path.GetTempPath(), SERVICENAME, "Temp");
        }

        const string SERVICENAME = "NodeService.WindowsService";

        private void TryDeleteDaemonService()
        {
            try
            {
                const string DaemonServiceDirectory = "C:\\shouhu\\DaemonService";
                ServiceHelper.Uninstall("JobsWorkerDaemonServiceWindowsService");
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    TryDeleteDaemonService();
                    EnsureTempPath();
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
                        continue;
                    }

                    var clientUpdateConfig = apiResult.Result;
                    if (clientUpdateConfig == null || clientUpdateConfig.PackageConfig == null)
                    {
                        _logger.LogError("invalid client config");
                        continue;
                    }
                    _logger.LogInformation($"Query config: {clientUpdateConfig.ToJsonString<ClientUpdateConfigModel>()}");
                    var destDirectory = Path.Combine(_currentConfig.InstallDirectory);
                    var updateConfigFilePath = Path.Combine(_currentConfig.InstallDirectory, "UpdateConfig");
                    var tempFileName = Path.Combine(_tempPath, Guid.NewGuid().ToString());
                    try
                    {
                        if (TryCompareConfig(clientUpdateConfig, updateConfigFilePath))
                        {
                            _logger.LogInformation("Same config,skip update");
                            continue;
                        }


                        using var stream = File.Open(tempFileName, FileMode.CreateNew, FileAccess.ReadWrite);
                        await apiService.DownloadPackageAsync(clientUpdateConfig.PackageConfig, stream, stoppingToken);
                        _logger.LogInformation($"Download {clientUpdateConfig}");
                        await apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
                        {
                            ClientUpdateConfigId = clientUpdateConfig.Id,
                            NodeName = Dns.GetHostName(),
                            CategoryName = "DownloadSuccess"
                        }, stoppingToken);
                        stream.Position = 0;
                        if (!IsZip(stream))
                        {
                            continue;
                        }
                        _logger.LogInformation($"Start Uninstall");
                        bool uninstalledResult = ServiceHelper.Uninstall(SERVICENAME);
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
                            continue;
                        }
                        var entryPoint = Path.Combine(destDirectory, clientUpdateConfig.PackageConfig.EntryPoint);
                        if (!File.Exists(entryPoint))
                        {
                            continue;
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
                        bool startResult = ServiceHelper.StartService(SERVICENAME,
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
                        if (File.Exists(tempFileName))
                        {
                            File.Delete(tempFileName);
                        }
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
        }

        private async ValueTask DelayAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_currentConfig.DurationMinutes), stoppingToken);
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

        private static bool IsZip(Stream stream)
        {
            var position = stream.Position;
            try
            {
                stream.Position = 0;
                byte[] zipBytes = [80, 75, 3, 4, 20, 0, 0, 0];
                byte[] bytes = new byte[8];
                int readCount = stream.Read(bytes);
                if (readCount == 8 && zipBytes.SequenceEqual(bytes))
                {
                    return true;
                }
            }
            finally
            {
                stream.Position = position;
            }

            return false;
        }

        private void EnsureTempPath()
        {
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
        }

        public override void Dispose()
        {
            _onChangeToken?.Dispose();
            base.Dispose();
        }
    }
}
