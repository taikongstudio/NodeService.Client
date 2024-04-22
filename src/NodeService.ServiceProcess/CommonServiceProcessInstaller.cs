﻿namespace NodeService.ServiceProcess
{
    public class CommonServiceProcessInstaller : IDisposable
    {
        private readonly ServiceProcessInstaller _serviceProcessInstaller;
        private PackageProvider _packageProvider;
        private ServiceProcessInstallContext _installContext;
        private MemoryStream _stream;

        private enum InstallState
        {
            Uninstall,
            Install,
            Commit,
            Rollback,
            Error,
        }

        private InstallState _state;

        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<InstallerProgressEventArgs> Failed;

        public event EventHandler<InstallerProgressEventArgs> ProgressChanged;

        public event EventHandler<InstallerProgressEventArgs> Completed;

        private CommonServiceProcessInstaller(
            ServiceProcessInstaller serviceProcessInstaller)
        {
            _serviceProcessInstaller = serviceProcessInstaller;
            AttachEvents();
        }

        private void AttachEvents()
        {
            _serviceProcessInstaller.BeforeInstall += _serviceProcessInstaller_BeforeInstall;
            _serviceProcessInstaller.AfterInstall += _serviceProcessInstaller_AfterInstall;
            _serviceProcessInstaller.BeforeUninstall += _serviceProcessInstaller_BeforeUninstall;
            _serviceProcessInstaller.AfterUninstall += _serviceProcessInstaller_AfterUninstall; ;
            _serviceProcessInstaller.Committing += _serviceProcessInstaller_Committing;
            _serviceProcessInstaller.Committed += _serviceProcessInstaller_Committed;
            _serviceProcessInstaller.BeforeRollback += _serviceProcessInstaller_BeforeRollback;
            _serviceProcessInstaller.AfterRollback += _serviceProcessInstaller_AfterRollback;
        }

        private void DetachEvents()
        {

            _serviceProcessInstaller.BeforeInstall -= _serviceProcessInstaller_BeforeInstall;
            _serviceProcessInstaller.AfterInstall -= _serviceProcessInstaller_AfterInstall;
            _serviceProcessInstaller.BeforeUninstall -= _serviceProcessInstaller_BeforeUninstall;
            _serviceProcessInstaller.AfterUninstall -= _serviceProcessInstaller_AfterUninstall; ;
            _serviceProcessInstaller.Committing -= _serviceProcessInstaller_Committing;
            _serviceProcessInstaller.Committed -= _serviceProcessInstaller_Committed;
            _serviceProcessInstaller.BeforeRollback -= _serviceProcessInstaller_BeforeRollback;
            _serviceProcessInstaller.AfterRollback -= _serviceProcessInstaller_AfterRollback;
        }

        private void _serviceProcessInstaller_AfterUninstall(object sender, InstallEventArgs e)
        {
            RaiseProgressChangedEvent("卸载成功");
            RaiseProgressChangedEvent($"正在清理目录\"{this._installContext.InstallDirectory}\"");
            if (!CleanupInstallDirectory())
            {
                return;
            }
            RaiseProgressChangedEvent("清理成功");
        }

        private bool IsQuickMode()
        {
            if (Debugger.IsAttached)
            {
                return Environment.GetEnvironmentVariable("QuickMode") == "1";
            }
            return false;
        }

        private bool CleanupInstallDirectory()
        {
            try
            {
                if (IsQuickMode())
                {
                    return true;
                }
                var installDirectory = new DirectoryInfo(this._installContext.InstallDirectory);
                if (!installDirectory.Exists)
                {
                    return true;
                }
                foreach (var directoryInfo in installDirectory.GetDirectories())
                {
                    if (directoryInfo.Name == ".package")
                    {
                        continue;
                    }
                    if (Directory.Exists(directoryInfo.FullName))
                    {
                        Directory.Delete(directoryInfo.FullName, true);
                    }
                    else
                    {
                        directoryInfo.Delete();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"清理目录\"{this._installContext.InstallDirectory}\"时发生了错误:{ex.ToString()}";
                RaiseFailedEvent(errorMessage);
                Cancel();
            }
            return false;
        }

        public void SetParameters(PackageProvider packageProvider, ServiceProcessInstallContext context)
        {
            _packageProvider = packageProvider;
            this._installContext = context;
        }

        private void _serviceProcessInstaller_AfterRollback(object sender, InstallEventArgs e)
        {

        }

        private void _serviceProcessInstaller_BeforeRollback(object sender, InstallEventArgs e)
        {

        }

        private void _serviceProcessInstaller_Committed(object sender, InstallEventArgs e)
        {

        }

        private void _serviceProcessInstaller_Committing(object sender, InstallEventArgs e)
        {

        }

        private void _serviceProcessInstaller_BeforeUninstall(object sender, InstallEventArgs e)
        {
            var message = $"正在卸载服务\"{_installContext.ServiceName}\"";
            RaiseProgressChangedEvent(message);
        }

        private void _serviceProcessInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            CheckServiceStatusAsync().Wait();
        }

        private async Task CheckServiceStatusAsync()
        {
            try
            {
                if (this._cancellationTokenSource != null && this._cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
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
                        await ExtractPackageToInstallDirectoryAsync(false);
                        RaiseProgressChangedEvent($"\"{_installContext.ServiceName}\"状态：{serviceController.Status}，尝试启动服务");
                        serviceController.Start();
                        RaiseProgressChangedEvent($"已执行启动操作");
                    }
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    stopwatch.Stop();
                    RaiseProgressChangedEvent($"持续观察服务\"{_installContext.ServiceName}\"状态，第{waitCount}次，等待：{stopwatch.Elapsed}");
                    stopwatch.Reset();
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    waitCount++;
                } while (waitCount < 12);
                RaiseCompletedEvent("安装成功");
            }
            catch (Exception ex)
            {
                string errorMessage = $"启动服务\"{_installContext.ServiceName}\"时发生了错误:{ex.Message}";
                RaiseFailedEvent(errorMessage);
                Cancel();
            }
        }

        private void _serviceProcessInstaller_BeforeInstall(object sender, InstallEventArgs e)
        {
            if (!DeployPackageImpl().Result)
            {
                throw new InvalidOperationException();
            }
        }

        private async Task<bool> DeployPackageImpl()
        {
            try
            {
                if (IsQuickMode())
                {
                    return true;
                }
                ResetStream();
                RaiseProgressChangedEvent($"开始安装");

                RaiseProgressChangedEvent($"开始下载服务\"{_installContext.ServiceName}\"的安装包");
                if (await _packageProvider.DownloadAsync(_stream) == false)
                {
                    RaiseFailedEvent("下载文件失败");
                    Cancel();
                    return false;
                }
                RaiseProgressChangedEvent($"下载成功，大小:{_stream.Length}");
                return await ExtractPackageToInstallDirectoryAsync();
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.Message);
                Cancel();
            }
            finally
            {
                DisposeStream();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            return false;
        }

        private void DisposeStream()
        {
            if (this._stream != null)
            {
                this._stream.Dispose();
                this._stream = null;
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
            GC.Collect();
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
            var serviceInstaller = ServiceProcessInstallerHelper.Create(serviceName, displayName, description, filePath, cmdLine);
            return new CommonServiceProcessInstaller(serviceInstaller);
        }

        private Task<bool> ExtractPackageToInstallDirectoryAsync(bool autoCancel = true)
        {
            return Task.Run<bool>(async () =>
            {
                int retryCount = 0;
            LRetry:
                try
                {
                    switch (retryCount)
                    {
                        case 1:
                            RaiseProgressChangedEvent("文件被占用，尝试杀死目录内可执行文件启动的进程。");
                            var executableFile = Directory.GetFiles(_installContext.InstallDirectory, "*.exe",
                            new EnumerationOptions()
                            {
                                RecurseSubdirectories = true,
                                MatchCasing = MatchCasing.PlatformDefault
                            }
                            );
                            foreach (var file in executableFile)
                            {
                                var moduleName = Path.GetFileName(file);
                                var processName = Path.GetFileNameWithoutExtension(file);
                                RaiseProgressChangedEvent($"尝试关闭相关进程:{processName}");
                                var processes = Process.GetProcessesByName(processName);
                                RaiseProgressChangedEvent($"可执行文件\"{processName}\"有{processes.Length}个相关进程");
                                foreach (var process in processes)
                                {
                                    var installerPath = Path.GetFullPath(_installContext.InstallDirectory);
                                    var directory = Path.GetFullPath(Path.GetDirectoryName(process.MainModule.FileName));
                                    if (process.MainModule.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase) &&
                                    directory.Equals(installerPath, StringComparison.OrdinalIgnoreCase)
                                    )
                                    {
                                        process.Kill(true);
                                        RaiseProgressChangedEvent($"杀死进程\"{process.ProcessName}\"");
                                    }

                                }
                            }
                            break;
                        case 10:
                            if (TryWriteExitFile())
                            {
                                await Task.Delay(TimeSpan.FromSeconds(10));
                            }
                            break;
                        default:
                            break;
                    }

                    if (IsQuickMode())
                    {
                        return true;
                    }
                    RaiseProgressChangedEvent($"开始解压包到目录:\"{_installContext.InstallDirectory}\"");
                    _stream.Position = 0;
                    
                    ZipFile.ExtractToDirectory(_stream, _installContext.InstallDirectory, true);
                    RaiseProgressChangedEvent($"解压成功");
                    return true;
                }
                catch (IOException ex)
                {
                    if (retryCount < 10)
                    {
                        RaiseProgressChangedEvent(ex.Message);
                        retryCount++;
                        goto LRetry;
                    }
                    else
                    {
                        RaiseFailedEvent(ex.Message);
                        Cancel();
                    }

                }
                catch (Exception ex)
                {
                    string errorMessage = $"解压文件到{_installContext.InstallDirectory}时发生了错误:{ex}";
                    if (autoCancel)
                    {
                        RaiseFailedEvent(errorMessage);
                        Cancel();
                    }
                }
                return false;
            });

        }

        private bool TryWriteExitFile()
        {
            try
            {
                var exitTxtFile = Path.Combine(_installContext.InstallDirectory, "exit.txt");
                File.WriteAllText(exitTxtFile, string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                RaiseProgressChangedEvent(ex.Message);
            }
            return false;
        }

        public Task<bool> RunAsync(CancellationToken cancellationToken = default)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            cancellationToken.Register(Cancel);
            return Task.Run<bool>(RunInstallLoopImpl);
        }

        private bool RunInstallLoopImpl()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            ListDictionary state = new ListDictionary();
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    switch (this._state)
                    {
                        case InstallState.Uninstall:
                            if (!UninstallImpl())
                            {
                                this._state = InstallState.Rollback;
                                continue;
                            }
                            this._state = InstallState.Install;
                            break;
                        case InstallState.Install:
                            if (!InstallImpl(state))
                            {
                                this._state = InstallState.Rollback;
                                continue;
                            }
                            this._state = InstallState.Commit;
                            break;
                        case InstallState.Commit:
                            CommitImpl(state);
                            goto LExit;
                        case InstallState.Rollback:
                            RollBackImpl(state);
                            break;
                        default:
                            break;
                    }
                }
               LExit:
                return this._state == InstallState.Commit;
            }
            catch (Exception ex)
            {
                RollBackImpl(state);
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            return false;
        }

        private bool RollBackImpl(ListDictionary listDictionary)
        {
            try
            {
                _serviceProcessInstaller.Rollback(listDictionary);
                return true;
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.ToString());
                Cancel();
            }
            return false;
        }

        private bool CommitImpl(ListDictionary listDictionary)
        {
            try
            {
                _serviceProcessInstaller.Commit(listDictionary);
                return true;
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.ToString());
                Cancel();
            }
            return false;
        }

        private bool InstallImpl(ListDictionary listDictionary)
        {
            try
            {
                _serviceProcessInstaller.Install(listDictionary);
                return true;
            }
            catch (Exception ex)
            {
                RaiseFailedEvent(ex.ToString());
                Cancel();
            }
            return false;
        }

        private bool UninstallImpl()
        {
            try
            {
                _serviceProcessInstaller.Uninstall(null);
                RaiseProgressChangedEvent("卸载成功");
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
            if (this._stream != null)
            {
                this._stream.Dispose();
                this._stream = null;
            }
            DetachEvents();
            this._serviceProcessInstaller.Dispose();
        }
    }
}