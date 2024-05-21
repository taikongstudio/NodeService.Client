using NodeService.Infrastructure;
using NodeService.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NodeService.ServiceProcess
{


    public class ServiceProcessRecovey : IDisposable
    {

        private readonly ILogger<ServiceProcessRecovey> _logger;
        private readonly ApiService _apiService;
        private ActionBlock<ServiceProcessInstallerProgress> _uploadActionBlock;
        private string _clientUpdateId;

        public ServiceProcessRecovey(ILogger<ServiceProcessRecovey> logger, ServiceProcessRecoveryContext recoveryContext)
        {
            _logger = logger;
            var httpAddress = recoveryContext.HttpAddress ?? "http://172.27.242.223:50060/";
            _apiService = new ApiService(new HttpClient()
            {
                BaseAddress = new Uri(httpAddress),
                Timeout = TimeSpan.FromSeconds(100)
            });
            RecoveryContext = recoveryContext;
            _uploadActionBlock = new ActionBlock<ServiceProcessInstallerProgress>(UploadAsync);
        }

        private async Task UploadAsync(ServiceProcessInstallerProgress progress)
        {
            await _apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
            {
                ClientUpdateConfigId = progress.ClientUpdateId,
                NodeName = Dns.GetHostName(),
                CategoryName = progress.Message
            });
        }

        public ServiceProcessRecoveryContext RecoveryContext { get; private set; }

        public Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return ExecuteCoreAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return Task.FromResult(false);
        }

        private async Task<bool> ExecuteCoreAsync(CancellationToken cancellationToken = default)
        {
            PackageDatabase? targetServiceDatabase = null;
            PackageDatabase? currentServiceDatabase = null;
            try
            {
                var currentServiceDatabasePath = CurrentServicePackageDirectory();
                if (!PackageDatabase.TryOpen(currentServiceDatabasePath, out currentServiceDatabase))
                {
                    _logger.LogInformation($"打开数据库失败:{currentServiceDatabasePath}");
                    return false;
                }
                var targetServiceDatabasePath = EnsurePackageDirectory();
                if (!PackageDatabase.TryOpen(targetServiceDatabasePath, out targetServiceDatabase))
                {
                    _logger.LogInformation($"打开数据库失败:{targetServiceDatabasePath}");
                    return false;
                }
                _logger.LogInformation($"打开目标服务\"{RecoveryContext.ServiceName}\"数据库成功:{targetServiceDatabasePath}");
                using ServiceController serviceController = new ServiceController(RecoveryContext.ServiceName);
                if (serviceController.Status == ServiceControllerStatus.Stopped)
                {
                    _logger.LogInformation($"{RecoveryContext.ServiceName}:{serviceController.Status}");
                    await Delay(cancellationToken);
                }
                serviceController.Refresh();
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"状态:{serviceController.Status}");
                switch (serviceController.Status)
                {
                    case ServiceControllerStatus.Stopped:
                        bool isInstalled = await ReinstallAsync(true, cancellationToken);
                        if (!isInstalled)
                        {
                            serviceController.Start();
                            _logger.LogInformation($"尝试启动服务\"{RecoveryContext.ServiceName}\"");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                        break;
                    case ServiceControllerStatus.Running:
                        return await ReinstallAsync(false, cancellationToken);
                    default:
                        break;
                }
                serviceController.Refresh();
                return serviceController.Status == ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is Win32Exception win32Exception && win32Exception.NativeErrorCode == 1060)
                {
                    _logger.LogInformation(ex.Message);
                    return await ReinstallAsync(true, cancellationToken);
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
            finally
            {
                if (targetServiceDatabase != null)
                {
                    targetServiceDatabase.Dispose();
                    _logger.LogInformation($"释放服务\"{RecoveryContext.ServiceName}\"数据库:{targetServiceDatabase.FullName}");
                }
                if (currentServiceDatabase != null)
                {
                    currentServiceDatabase.Dispose();
                    _logger.LogInformation($"释放服务当前服务的数据库:{currentServiceDatabase.FullName}");
                }
            }
            return false;
        }

        private async Task<bool> ReinstallAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation($"安装\"{RecoveryContext.ServiceName}\"，强制：{force}");
                bool quickMode = false;
                if (Debugger.IsAttached && quickMode)
                {
                    Environment.SetEnvironmentVariable("QuickMode", "1");
                }
                PackageConfigModel? packageConfig = await FetchPackageUpdateAsync(force, cancellationToken);

                if (packageConfig == null)
                {
                    return false;
                }

                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"开始安装");
                var filePath = Path.Combine(RecoveryContext.InstallDirectory, packageConfig.EntryPoint);
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"入口点为：{filePath}");
                using var installer = CommonServiceProcessInstaller.Create(
                    RecoveryContext.ServiceName,
                    RecoveryContext.DisplayName,
                    RecoveryContext.Description,
                    filePath,
                    RecoveryContext.Arguments);

                installer.SetParameters(
                    new HttpPackageProvider(_apiService, packageConfig),
                    new ServiceProcessInstallContext(RecoveryContext.ServiceName,
                        RecoveryContext.DisplayName,
                        RecoveryContext.Description,
                        RecoveryContext.InstallDirectory));

                installer.ProgressChanged += Installer_ProgressChanged;
                installer.Failed += Installer_Failed;
                installer.Completed += Installer_Completed;

                bool result = await installer.RunAsync(cancellationToken);
                if (result && TryWritePackageInfo(packageConfig))
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return false;
        }

        private async Task<PackageConfigModel?> FetchPackageUpdateAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation($"查询服务\"{RecoveryContext.ServiceName}\"的更新配置");
                var rsp = await _apiService.QueryClientUpdateAsync(RecoveryContext.ServiceName);
                if (rsp.ErrorCode != 0)
                {
                    _logger.LogError($"查询客户端更新配置失败：{rsp.Message}");
                    return null;
                }

                var clientUpdateConfig = rsp.Result;
                if (clientUpdateConfig == null)
                {
                    _logger.LogError($"查询更新失败:{RecoveryContext.ServiceName}");
                    return null;
                }
                _logger.LogInformation($"查询服务\"{RecoveryContext.ServiceName}\"的更新配置成功");
                _logger.LogInformation(clientUpdateConfig.ToJson<ClientUpdateConfigModel>());
                var packageConfig = clientUpdateConfig.PackageConfig;
                if (packageConfig == null)
                {
                    _logger.LogError($"包内容缺失:{RecoveryContext.ServiceName}");
                    return null;
                }
                if (packageConfig.Hash == null)
                {
                    _logger.LogError($"包哈希值缺失");
                    return null;
                }
                _logger.LogInformation($"查询服务\"{RecoveryContext.ServiceName}\"的包配置成功");
                if (!force && TryValidatePackage(packageConfig))
                {
                    _logger.LogWarning($"服务\"{RecoveryContext.ServiceName}\"跳过更新");
                    return null;
                }
                _logger.LogWarning($"服务\"{RecoveryContext.ServiceName}\"获取到更新");
                _clientUpdateId = clientUpdateConfig.Id;
                return packageConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return null;
        }

        private bool TryValidatePackage(PackageConfigModel packageConfig)
        {
            try
            {
                string packageDirectory = EnsurePackageDirectory();
                var packageFilePath = Path.Combine(packageDirectory, RecoveryContext.ServiceName);
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"开始对比Hash");
                if (File.Exists(packageFilePath))
                {
                    var jsonString = File.ReadAllText(packageFilePath);
                    _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"读取本地包信息：{jsonString}");
                    var localPackage = JsonSerializer.Deserialize<PackageConfigModel>(jsonString);
                    if (localPackage == null)
                    {
                        return false;
                    }
                    bool isSameHash = packageConfig.Hash == localPackage.Hash;
                    var entryPointFileName = Path.Combine(RecoveryContext.InstallDirectory, packageConfig.EntryPoint);
                    bool isEntryPointExits = File.Exists(entryPointFileName);
                    return isSameHash && isEntryPointExits;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return false;
        }

        private bool TryWritePackageInfo(PackageConfigModel  packageConfig)
        {
            try
            {
                string packageDirectory = EnsurePackageDirectory();
                var packageFilePath = Path.Combine(packageDirectory, RecoveryContext.ServiceName);
                var jsonString = JsonSerializer.Serialize(packageConfig);
                File.WriteAllText(packageFilePath, jsonString);
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"写包信息：{jsonString}到文件{packageFilePath}成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"写入包信息失败");
            }
            return false;
        }

        private string EnsurePackageDirectory()
        {
            var packageDirectory = Path.Combine(RecoveryContext.InstallDirectory, ".package");
            return packageDirectory;
        }

        private string CurrentServicePackageDirectory()
        {
            var packageDirectory = Path.Combine(AppContext.BaseDirectory, ".package");
            return packageDirectory;
        }

        private void Installer_Completed(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogInformation(e.Progress.Message);
            e.Progress.ClientUpdateId = _clientUpdateId;
            this._uploadActionBlock.Post(e.Progress);
        }

        private void Installer_Failed(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogError(e.Progress.Message);
            e.Progress.ClientUpdateId = _clientUpdateId;
            this._uploadActionBlock.Post(e.Progress);
        }

        private void Installer_ProgressChanged(object? sender, InstallerProgressEventArgs e)
        {
            _logger.LogInformation(e.Progress.Message);
            e.Progress.ClientUpdateId = _clientUpdateId;
            this._uploadActionBlock.Post(e.Progress);
        }

        private async Task Delay(CancellationToken stoppingToken)
        {
            try
            {
                var timeSpan =
                    Debugger.IsAttached ?
                    TimeSpan.Zero :
                    TimeSpan.FromSeconds(Random.Shared.Next(10, 30));
                _logger.LogInformation($"等待{timeSpan}");
                await Task.Delay(timeSpan, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        public void Dispose()
        {
            if (this._apiService != null)
            {
                this._apiService.Dispose();
            }
            if (this._uploadActionBlock != null)
            {
                this._uploadActionBlock.Complete();
            }
        }
    }
}
