using System.Collections;

namespace NodeService.ServiceProcess
{
    public class CommonServiceProcessInstaller : IDisposable
    {

        private readonly Installer _installer;
        private PackageProvider _packageProvider;
        private ServiceProcessInstallContext _installContext;
        private Stream? _stream;
        private IDictionary _installerState;
        private int _deployErrorCount = 0;

        private enum InstallState
        {
            Initial,
            Uninstalled,
            Installed,
            Commited,
            Finished,
        }

        private InstallState _state;

        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<InstallerProgressEventArgs> Failed;

        public event EventHandler<InstallerProgressEventArgs> ProgressChanged;

        public event EventHandler<InstallerProgressEventArgs> Completed;

        private CommonServiceProcessInstaller(
            Installer installer)
        {
            _installerState = new ListDictionary();
            _installer = installer;
            _state = InstallState.Initial;
            AttachEvents();
        }

        private void AttachEvents()
        {
            _installer.BeforeInstall += _serviceProcessInstaller_BeforeInstall;
            _installer.AfterInstall += _serviceProcessInstaller_AfterInstall;
            _installer.BeforeUninstall += _serviceProcessInstaller_BeforeUninstall;
            _installer.AfterUninstall += _serviceProcessInstaller_AfterUninstall; ;
            _installer.Committing += _serviceProcessInstaller_Committing;
            _installer.Committed += _serviceProcessInstaller_Committed;
            _installer.BeforeRollback += _serviceProcessInstaller_BeforeRollback;
            _installer.AfterRollback += _serviceProcessInstaller_AfterRollback;
        }

        private void DetachEvents()
        {

            _installer.BeforeInstall -= _serviceProcessInstaller_BeforeInstall;
            _installer.AfterInstall -= _serviceProcessInstaller_AfterInstall;
            _installer.BeforeUninstall -= _serviceProcessInstaller_BeforeUninstall;
            _installer.AfterUninstall -= _serviceProcessInstaller_AfterUninstall; ;
            _installer.Committing -= _serviceProcessInstaller_Committing;
            _installer.Committed -= _serviceProcessInstaller_Committed;
            _installer.BeforeRollback -= _serviceProcessInstaller_BeforeRollback;
            _installer.AfterRollback -= _serviceProcessInstaller_AfterRollback;
        }

        private void _serviceProcessInstaller_AfterUninstall(object sender, InstallEventArgs e)
        {
            RaiseProgressChangedEvent($"卸载服务\"{_installContext.ServiceName}\"成功");
        }

        private bool BackupInstallDirectory()
        {
            try
            {
                var installDirectory = new DirectoryInfo(this._installContext.InstallDirectory);
                if (!installDirectory.Exists)
                {
                    return true;
                }
                var backupDirectory = Path.TrimEndingDirectorySeparator(installDirectory.FullName) + "_backup";

                if (Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, true);
                }
                CopyDirectory(installDirectory, Directory.CreateDirectory(backupDirectory));

                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"备份目录\"{this._installContext.InstallDirectory}\"时发生了错误:{ex}";
                RaiseProgressChangedEvent(errorMessage);
            }
            return false;
        }

        private bool RestoreInstallDirectory()
        {
            try
            {
                var installDirectory = new DirectoryInfo(this._installContext.InstallDirectory);
                if (!installDirectory.Exists)
                {
                    return true;
                }
                var backupDirectory = Path.TrimEndingDirectorySeparator(installDirectory.FullName) + "_backup";

                if (!Directory.Exists(backupDirectory))
                {
                    return false;
                }
                CopyDirectory(new DirectoryInfo(backupDirectory), installDirectory);
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"恢复目录\"{this._installContext.InstallDirectory}\"时发生了错误:{ex}";
                RaiseProgressChangedEvent(errorMessage);
            }
            finally
            {

            }
            return false;
        }

        private bool CleanupInstallDirectory()
        {
            try
            {
                var installDirectory = new DirectoryInfo(this._installContext.InstallDirectory);
                if (!installDirectory.Exists)
                {
                    return true;
                }
                var backupDirectory = Path.TrimEndingDirectorySeparator(installDirectory.FullName) + "_backup";

                if (!Directory.Exists(backupDirectory))
                {
                    return false;
                }
                Directory.Delete(backupDirectory, true);
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"清理\"{this._installContext.InstallDirectory}\"的备份目录时发生了错误:{ex}";
                RaiseProgressChangedEvent(errorMessage);
            }
            finally
            {

            }
            return false;
        }

        private static void CopyDirectory(DirectoryInfo installDirectory, DirectoryInfo backupDirectory)
        {
            foreach (var file in installDirectory.GetFiles("*", new EnumerationOptions() { RecurseSubdirectories = true }))
            {
                if (file.Directory.Name == ".package")
                {
                    continue;
                }
                var relativePath = Path.GetRelativePath(installDirectory.FullName, file.FullName);
                var destFilePath = Path.Combine(backupDirectory.FullName, relativePath);
                var directory = Path.GetDirectoryName(destFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(file.FullName, destFilePath, true);

            }
        }

        public void SetParameters(PackageProvider packageProvider, ServiceProcessInstallContext context)
        {
            _packageProvider = packageProvider;
            this._installContext = context;
        }

        private void _serviceProcessInstaller_AfterRollback(object sender, InstallEventArgs e)
        {
            var message = $"服务\"{_installContext.ServiceName}\"回滚完成";
            RaiseProgressChangedEvent(message);
        }

        private void _serviceProcessInstaller_BeforeRollback(object sender, InstallEventArgs e)
        {
            var message = $"服务\"{_installContext.ServiceName}\"开始回滚";
            RaiseProgressChangedEvent(message);

        }

        private void _serviceProcessInstaller_Committed(object sender, InstallEventArgs e)
        {
            var message = $"服务\"{_installContext.ServiceName}\"已提交";
            RaiseProgressChangedEvent(message);
        }

        private void _serviceProcessInstaller_Committing(object sender, InstallEventArgs e)
        {
            var message = $"服务\"{_installContext.ServiceName}\"提交中";
            RaiseProgressChangedEvent(message);
        }

        private void _serviceProcessInstaller_BeforeUninstall(object sender, InstallEventArgs e)
        {
            var message = $"正在卸载服务\"{_installContext.ServiceName}\"";
            RaiseProgressChangedEvent(message);
        }

        private void _serviceProcessInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            bool hasBackgup = BackupInstallDirectory();
            bool deployPackageResult = false;
            try
            {
                if (this._deployErrorCount > 10)
                {
                    this.Cancel();
                    return;
                }
                deployPackageResult = DeployPackageAsync(_cancellationTokenSource.Token).Result;
                if (!deployPackageResult && hasBackgup)
                {
                    if (RestoreInstallDirectory())
                    {
                        RaiseProgressChangedEvent($"恢复服务\"{_installContext.ServiceName}\"目录成功");
                    }
                    else
                    {
                        RaiseProgressChangedEvent($"恢复服务\"{_installContext.ServiceName}\"目录失败");
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.ToString());
            }
            finally
            {
                if (CleanupInstallDirectory())
                {
                    RaiseProgressChangedEvent($"清理服务\"{_installContext.ServiceName}\"备份目录成功");
                }
                else
                {
                    RaiseProgressChangedEvent($"清理服务\"{_installContext.ServiceName}\"备份目录失败");
                }
            }
            if (!deployPackageResult)
            {
                _deployErrorCount++;
                RaiseProgressChangedEvent($"服务\"{_installContext.ServiceName}\"第{_deployErrorCount}次部署错误。");
                throw new InstallException();
            }
        }

        private bool CheckServiceStatus()
        {
            try
            {
                using ServiceController serviceController = new ServiceController(_installContext.ServiceName);
                serviceController.Start();
                RaiseProgressChangedEvent($"等待服务\"{_installContext.ServiceName}\"运行");
                Stopwatch stopwatch = Stopwatch.StartNew();
                int waitCount = 1;
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(180));
                stopwatch.Stop();
                RaiseProgressChangedEvent($"服务\"{_installContext.ServiceName}\"已运行，等待：{stopwatch.Elapsed}");
                do
                {
                    stopwatch.Restart();
                    serviceController.Refresh();
                    if (serviceController.Status != ServiceControllerStatus.Running)
                    {
                        RaiseProgressChangedEvent($"\"{_installContext.ServiceName}\"状态：{serviceController.Status}，尝试启动服务");
                        serviceController.Start();
                        RaiseProgressChangedEvent($"已执行启动操作");
                    }
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(5));
                    stopwatch.Stop();
                    RaiseProgressChangedEvent($"持续观察服务\"{_installContext.ServiceName}\"状态，第{waitCount}次，等待：{stopwatch.Elapsed}");
                    stopwatch.Reset();
                    Thread.Sleep(5000);
                    waitCount++;
                } while (waitCount < 6);
                RaiseCompletedEvent("安装成功");
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"启动服务\"{_installContext.ServiceName}\"时发生了错误:{ex.Message}";
                RaiseFailedEvent(errorMessage);
                Cancel();
            }
            return false;
        }

        private void _serviceProcessInstaller_BeforeInstall(object sender, InstallEventArgs e)
        {
            RaiseProgressChangedEvent($"开始安装服务\"{_installContext.ServiceName}\"");
        }

        private async Task<bool> DeployPackageAsync(CancellationToken cancellationToken=default)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10) * this._deployErrorCount);
                ResetStream();
                RaiseProgressChangedEvent($"开始下载服务\"{_installContext.ServiceName}\"的安装包");
                if (!await _packageProvider.DownloadAsync(_stream))
                {
                    RaiseFailedEvent($"下载服务\"{_installContext.ServiceName}\"的安装包失败");
                    Cancel();
                    return false;
                }
                RaiseProgressChangedEvent($"下载服务\"{_installContext.ServiceName}\"的安装包成功，大小:{_stream.Length}");
                for (int i = 0; i < 10 && !cancellationToken.IsCancellationRequested; i++)
                {
                    if (ExtractPackageToInstallDirectory())
                    {
                        return true;
                    }
                    RaiseProgressChangedEvent($"解压服务\"{_installContext.ServiceName}\"的安装包，第{i + 1}次重试，隔30秒再次重试。");
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.Message);
                Cancel();
            }
            finally
            {
                DisposeStream();
            }
            return false;
        }

        private void DisposeStream()
        {
            if (this._stream != null)
            {
                this._stream.Dispose();
                this._stream = null;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
            }
        }

        public void Cancel()
        {
            if (this._cancellationTokenSource != null && !this._cancellationTokenSource.IsCancellationRequested)
            {
                this._cancellationTokenSource.Cancel();
            }
        }

        private void ResetStream()
        {
            DisposeStream();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            _stream = new MemoryStream();
        }

        private void RaiseFailedEvent(string message)
        {
            this.Failed?.Invoke(
                this,
                new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                    this._installContext.ServiceName,
                    ServiceProcessInstallerProgressType.Error,
                    message)));
        }

        private void RaiseProgressChangedEvent(string message)
        {
            this.ProgressChanged?.Invoke(
                this,
                new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                    this._installContext.ServiceName,
                    ServiceProcessInstallerProgressType.Info,
                    message)));
        }

        private void RaiseCompletedEvent(string message)
        {
            this.Completed?.Invoke(
                this,
                new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                    this._installContext.ServiceName,
                    ServiceProcessInstallerProgressType.Info,
                    message)));
        }

        public static CommonServiceProcessInstaller Create(
            string serviceName,
            string displayName,
            string description,
            string filePath,
            string cmdLine
            )
        {
            var serviceInstaller = ServiceProcessInstallerHelper.CreateTransactedInstaller(serviceName, displayName, description, filePath, cmdLine);
            return new CommonServiceProcessInstaller(serviceInstaller);
        }

        private bool ExtractPackageToInstallDirectory(bool autoCancel = true)
        {
            try
            {
                RaiseProgressChangedEvent($"开始解压包到目录:\"{_installContext.InstallDirectory}\"");
                if (_stream != null)
                {
                    _stream.Position = 0;
                    ZipFile.ExtractToDirectory(_stream, _installContext.InstallDirectory, Encoding.UTF8, true);
                    RaiseProgressChangedEvent($"解压成功");
                }

                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"解压文件到{_installContext.InstallDirectory}时发生了错误:{ex}";
                RaiseFailedEvent(errorMessage);
            }
            finally
            {

            }
            return false;
        }

        public Task<bool> RunAsync(CancellationToken cancellationToken = default)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            cancellationToken.Register(Cancel);
            return Task.Run<bool>(RunInstallLoopImpl);
        }

        private bool RunInstallLoopImpl()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    if (this._state == InstallState.Initial)
                    {
                        if (!TryUninstall(null))
                        {
                            break;
                        }
                        this._state = InstallState.Uninstalled;
                    }
                    else if (this._state == InstallState.Uninstalled)
                    {
                        if (!TryInstall(_installerState))
                        {
                            break;
                        }
                        this._state = InstallState.Commited;
                    }
                    else if (this._state == InstallState.Commited)
                    {
                        if (!CheckServiceStatus())
                        {
                            break;
                        }
                        this._state = InstallState.Finished;
                        break;
                    }
                }
                return this._state == InstallState.Finished;
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.ToString());
            }
            finally
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
            }
            return false;
        }

        private bool TryInstall(IDictionary state)
        {
            try
            {
                _installer.Install(state);
                return true;
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.ToString());
                Cancel();
            }
            return false;
        }

        private bool TryUninstall(IDictionary state)
        {
            try
            {
                _installer.Uninstall(state);
                return true;
            }
            catch (InstallException ex)
            {

                if (ex.InnerException is Win32Exception win32Exception && win32Exception.NativeErrorCode == 1060)
                {
                    RaiseProgressChangedEvent($"卸载失败:\"{win32Exception.Message}\"");
                    return true;
                }
                RaiseFailedEvent(ex.ToString());
                Cancel();
            }
            return false;
        }

        public void Dispose()
        {
            DisposeStream();
            if (this._cancellationTokenSource != null && !this._cancellationTokenSource.IsCancellationRequested)
            {
                this._cancellationTokenSource.Cancel();
            }
            DetachEvents();
            this._installer.Dispose();
        }
    }
}