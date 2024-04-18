using NodeService.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.ServiceProcess
{


    public class ServiceProcessDoctor : IDisposable
    {
        private class LockFileHolder : IDisposable
        {


            private readonly SafeHandle _fileHandle;

            public LockFileHolder(SafeHandle safeHandle,string fullName)
            {
                _fileHandle = safeHandle;
                FullName = fullName;
            }

            public string FullName {  get; private set; }

            public static bool TryLock(string lockFilePath, out LockFileHolder? holder)
            {
                holder = null;
                try
                {
                    var handle = File.OpenHandle(
                        lockFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        FileOptions.None);
                    holder = new LockFileHolder(handle, lockFilePath);
                    return true;
                }
                catch (Exception ex)
                {

                }
                return false;
            }

            public void Dispose()
            {
                if (this._fileHandle != null)
                {
                    this._fileHandle.Close();
                }
            }
        }

        private readonly ILogger<ServiceProcessDoctor> _logger;
        private readonly ApiService _apiService;


        public ServiceProcessDoctor(ILogger<ServiceProcessDoctor> logger, ServiceProcessRecoveryContext recoveryContext)
        {
            _logger = logger;
            RecoveryContext = recoveryContext;
            var httpAddress = RecoveryContext.HttpAddress ?? "http://172.27.242.223:50060/";
            _apiService = new ApiService(new HttpClient()
            {
                BaseAddress = new Uri(httpAddress),
                Timeout = TimeSpan.FromSeconds(100)
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
            LockFileHolder? updateLock = null;
            LockFileHolder? currentServiceLock = null;
            try
            {
                var currentServiceLockPath = Path.Combine(EnsureCurrentServicePackageDirectory(), ".lock");
                if (!LockFileHolder.TryLock(currentServiceLockPath, out currentServiceLock))
                {
                    _logger.LogInformation($"Open lock file fail:{currentServiceLockPath}");
                    return false;
                }
                var lockFilePath = Path.Combine(EnsurePackageDirectory(), ".lock");
                if (!LockFileHolder.TryLock(lockFilePath, out updateLock))
                {
                    _logger.LogInformation($"Open lock file fail:{lockFilePath}");
                    return false;
                }
                _logger.LogInformation($"Open lock file success:{lockFilePath}");
                using ServiceController serviceController = new ServiceController(RecoveryContext.ServiceName);
                if (serviceController.Status == ServiceControllerStatus.Stopped)
                {
                    _logger.LogInformation($"{RecoveryContext.ServiceName}:{serviceController.Status}");
                    await Delay(cancellationToken);
                }
                serviceController.Refresh();
                _logger.LogInformation($"{RecoveryContext.ServiceName}:{serviceController.Status}");
                switch (serviceController.Status)
                {
                    case ServiceControllerStatus.Stopped:

                        return await ReinstallAsync(true, cancellationToken);
                    case ServiceControllerStatus.Running:
                        return await ReinstallAsync(false, cancellationToken);
                    default:
                        break;
                }
                return false;
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
                if (updateLock != null)
                {
                    updateLock.Dispose();
                    _logger.LogInformation($"释放服务\"{RecoveryContext.ServiceName}\"文件锁:{updateLock.FullName}");
                }
                if (currentServiceLock != null)
                {
                    currentServiceLock.Dispose();
                    _logger.LogInformation($"释放服务当前服务的文件锁:{currentServiceLock.FullName}");
                }
            }
            return false;
        }

        private async Task<bool> ReinstallAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            _logger.LogError($"安装\"{RecoveryContext.ServiceName}\"，强制：{force}");
            bool quickMode = false;
            if (Debugger.IsAttached && quickMode)
            {
                Environment.SetEnvironmentVariable("QuickMode", "1");
            }
            _logger.LogError($"查询服务\"{RecoveryContext.ServiceName}\"的更新配置");
            var rsp = await _apiService.QueryClientUpdateAsync(RecoveryContext.ServiceName);
            if (rsp.ErrorCode != 0)
            {
                _logger.LogError($"查询客户端更新配置失败：{rsp.Message}");
                return false;
            }

            var clientUpdateConfig = rsp.Result;
            if (clientUpdateConfig == null)
            {
                _logger.LogError($"Could not find update:{RecoveryContext.ServiceName}");
                return false;
            }
            _logger.LogError($"查询服务\"{RecoveryContext.ServiceName}\"的更新配置成功");
            _logger.LogInformation(clientUpdateConfig.ToJsonString<ClientUpdateConfigModel>());
            var packageConfig = clientUpdateConfig.PackageConfig;
            if (packageConfig == null)
            {
                _logger.LogError($"Could not find package:{RecoveryContext.ServiceName}");
                return false;
            }
            _logger.LogError($"查询服务\"{RecoveryContext.ServiceName}\"的包配置成功");
            if (!force && TryComparePackageHash(packageConfig.Hash))
            {
                _logger.LogError($"服务\"{RecoveryContext.ServiceName}\"跳过更新");
                return true;
            }
            _logger.LogError($"服务\"{RecoveryContext.ServiceName}\"开始安装");
            var filePath = Path.Combine(RecoveryContext.InstallDirectory, packageConfig.EntryPoint);
            _logger.LogError($"服务\"{RecoveryContext.ServiceName}\"入口点为：{filePath}");
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
            if (result && WritePackageHash(packageConfig.Hash))
            {
                return true;
            }
            return false;
        }

        private bool TryComparePackageHash(string packageHash)
        {
            try
            {
                var hashFilePath = Path.Combine(RecoveryContext.InstallDirectory, ".package", "PackageHash");
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"开始对比Hash");
                if (File.Exists(hashFilePath))
                {
                    var hash = File.ReadAllText(hashFilePath);
                    _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"读取本地Hash值：{hash}, 包Hash值：{packageHash}");
                    return hash == packageHash;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        private bool WritePackageHash(string hash)
        {
            try
            {
                string packageDirectory = EnsurePackageDirectory();
                var hashFilePath = Path.Combine(packageDirectory, "PackageHash");
                File.WriteAllText(hashFilePath, hash);
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"写入Hash值{hash}到文件{hashFilePath}成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"服务\"{RecoveryContext.ServiceName}\"写入Hash值{hash}失败");
            }
            return false;
        }

        private string EnsurePackageDirectory()
        {
            var packageDirectory = Path.Combine(RecoveryContext.InstallDirectory, ".package");
            if (!Directory.Exists(packageDirectory))
            {
                Directory.CreateDirectory(packageDirectory);
            }

            return packageDirectory;
        }

        private string EnsureCurrentServicePackageDirectory()
        {
            var packageDirectory = Path.Combine(AppContext.BaseDirectory, ".package");
            if (!Directory.Exists(packageDirectory))
            {
                Directory.CreateDirectory(packageDirectory);
            }

            return packageDirectory;
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

        private async Task Delay(CancellationToken stoppingToken)
        {
            try
            {
                var timeSpan =
                    Debugger.IsAttached ?
                    TimeSpan.Zero :
                    TimeSpan.FromSeconds(Random.Shared.Next(1, 60));
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
        }
    }
}
