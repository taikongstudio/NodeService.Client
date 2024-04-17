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
    

    public class ServiceProcessDoctor:IDisposable
    {
        private class LockFileHolder : IDisposable
        {


            private SafeHandle _fileHandle;

            public LockFileHolder(SafeHandle  safeHandle)
            {
                _fileHandle = safeHandle;
            }

            public static bool TryLock(string lockFilePath, out LockFileHolder? holder)
            {
                holder = null;
                try
                {
                    var handle = File.OpenHandle(lockFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, FileOptions.None);
                    holder = new LockFileHolder(handle);
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
                    this._fileHandle.Dispose();
                }
            }
        }

        private readonly ILogger<ServiceProcessDoctor> _logger;
        private readonly ApiService _apiService;
        private readonly CommonServiceProcessInstaller _installer;
        private int _installFailedCount;


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

        private async Task<bool> ExecuteCoreAsync(CancellationToken cancellationToken=default)
        {
            LockFileHolder? holder = null;
            try
            {
                var lockFilePath = Path.Combine(EnsurePackageDirectory(), ".lock");
                if (!LockFileHolder.TryLock(lockFilePath, out holder))
                {
                    _logger.LogInformation("Open lock file fail");
                    return false;
                }
                using ServiceController serviceController = new ServiceController(RecoveryContext.ServiceName);
                if (serviceController.Status == ServiceControllerStatus.Stopped)
                {
                    await Delay(cancellationToken);
                }
                serviceController.Refresh();

                switch (serviceController.Status)
                {
                    case ServiceControllerStatus.Stopped:
                        return await ReinstallAsync(true, cancellationToken);
                        break;
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
                if (holder!=null)
                {
                    holder.Dispose();
                }
            }
            return false;
        }

        private async Task<bool> ReinstallAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            bool quickMode = false;
            if (Debugger.IsAttached && quickMode)
            {
                Environment.SetEnvironmentVariable("QuickMode", "1");
            }
            var rsp = await _apiService.QueryClientUpdateAsync(RecoveryContext.ServiceName);
            if (rsp.ErrorCode != 0)
            {
                _logger.LogError(rsp.Message);
                return false;
            }
            var clientUpdateConfig = rsp.Result;
            if (clientUpdateConfig == null)
            {
                _logger.LogError($"Could not find update:{RecoveryContext.ServiceName}");
                return false;
            }
            _logger.LogInformation(clientUpdateConfig.ToJsonString<ClientUpdateConfigModel>());
            var packageConfig = clientUpdateConfig.PackageConfig;
            if (packageConfig == null)
            {
                _logger.LogError($"Could not find package:{RecoveryContext.ServiceName}");
                return false;
            }
            if (!force && TryComparePackageHash(packageConfig.Hash))
            {
                return true;
            }

            var filePath = Path.Combine(RecoveryContext.InstallDirectory, packageConfig.EntryPoint);
            var installer = CommonServiceProcessInstaller.Create(
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

        private bool TryComparePackageHash(string hash)
        {
            try
            {
                var hashFilePath = Path.Combine(RecoveryContext.InstallDirectory, ".package", "PackageHash");
                if (File.Exists(hashFilePath))
                {
                    var oldHash = File.ReadAllText(hashFilePath);
                    return oldHash == hash;
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
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
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

        public void Dispose()
        {
            if (this._apiService != null)
            {
                this._apiService.Dispose();
            }
        }
    }
}
