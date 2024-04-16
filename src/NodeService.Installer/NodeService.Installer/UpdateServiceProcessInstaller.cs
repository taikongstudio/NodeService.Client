using FluentFTP;
using NodeService.Installer;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public class UpdateServiceProcessInstaller : IDisposable
{
    private readonly ServiceProcessInstaller _serviceProcessInstaller;
    private InstallConfig _installConfig;
    private MemoryStream _stream;
    private IProgress<FtpProgress> _progressProvider;

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

    private UpdateServiceProcessInstaller(ServiceProcessInstaller serviceProcessInstaller)
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
        RaiseProgressChangedEvent($"正在清理目录\"{this._installConfig.InstallPath}\"");
        if (!CleanupInstallDirectory())
        {
            return;
        }
        RaiseProgressChangedEvent("清理成功");
    }

    private bool CleanupInstallDirectory()
    {
        try
        {
            var installDirectory = new DirectoryInfo(this._installConfig.InstallPath);
            if (!installDirectory.Exists)
            {
                return true;
            }
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

            return true;
        }
        catch (Exception ex)
        {
            string errorMessage = $"清理目录\"{this._installConfig.PackagePath}\"时发生了错误:{ex.ToString()}";
            RaiseFailedEvent(errorMessage);
            Cancel();
        }
        return false;
    }

    public void SetInstallConfig(InstallConfig installConfig)
    {
        this._installConfig = installConfig;
    }

    public void SetFileDownloadProgressProvider(IProgress<FtpProgress> progressProvider)
    {
        _progressProvider = progressProvider;
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
        var message = $"正在卸载服务\"{_installConfig.ServiceName}\"";
        RaiseProgressChangedEvent(message);
    }

    private void _serviceProcessInstaller_AfterInstall(object sender, InstallEventArgs e)
    {
        try
        {
            using ServiceController serviceController = new ServiceController(_installConfig.ServiceName);
            serviceController.Start();
            RaiseProgressChangedEvent($"等待服务\"{_installConfig.ServiceName}\"运行");
            Stopwatch stopwatch = Stopwatch.StartNew();
            serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(180));
            stopwatch.Stop();
            RaiseProgressChangedEvent($"服务\"{_installConfig.ServiceName}\"已运行，等待：{stopwatch.Elapsed}");
            RaiseCompletedEvent("安装成功");
        }
        catch (Exception ex)
        {
            string errorMessage = $"启动服务\"{_installConfig.ServiceName}\"时发生了错误:{ex.Message}";
            RaiseFailedEvent(errorMessage);
            Cancel();
        }
    }

    private void _serviceProcessInstaller_BeforeInstall(object sender, InstallEventArgs e)
    {
        DownloadPackageImpl().Wait();
    }

    private async Task DownloadPackageImpl()
    {
        try
        {
            RaiseProgressChangedEvent($"开始安装");
            using var ftpClient = new AsyncFtpClient(
                     _installConfig.Host,
                     _installConfig.Username,
                     _installConfig.Password,
                     _installConfig.Port
                     );
            await ftpClient.AutoConnect();
            string message = string.Empty;
            if ((await ftpClient.FileExists(_installConfig.PackagePath)) == false)
            {
                message = "服务器不存在此文件";
                RaiseFailedEvent(message);
                Cancel();
                return;
            }
            InitStream();

            message = $"Ftp服务器：{this._installConfig.Host}，开始下载文件：{_installConfig.PackagePath}";
            RaiseProgressChangedEvent(message);
            if (!await ftpClient.DownloadStream(_stream, _installConfig.PackagePath, progress: this._progressProvider))
            {
                string errorMessage = "下载安装包失败";
                RaiseFailedEvent(errorMessage);
                return;
            }
            RaiseProgressChangedEvent($"下载成功，大小:{_stream.Length}");
            _stream.Position = 0;
            await ExtractPackageToInstallDirectoryAsync();
        }
        catch (Exception ex)
        {
            RaiseFailedEvent(ex.Message);
            Cancel();
        }

    }

    public void Cancel()
    {
        if (this._cancellationTokenSource != null && !this._cancellationTokenSource.IsCancellationRequested)
        {
            this._cancellationTokenSource.Cancel();
        }
    }

    private void InitStream()
    {
        if (this._stream != null)
        {
            this._stream.Dispose();
        }
        _stream = new MemoryStream();
    }

    private void RaiseFailedEvent(string message)
    {
        this.Failed?.Invoke(
            this,
            new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                this._installConfig.ServiceName,
                ServiceProcessInstallerProgressType.Error,
                message)));
    }

    private void RaiseProgressChangedEvent(string message)
    {
        this.ProgressChanged?.Invoke(
            this,
            new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                this._installConfig.ServiceName,
                ServiceProcessInstallerProgressType.Info,
                message)));
    }

    private void RaiseCompletedEvent(string message)
    {
        this.Completed?.Invoke(
            this,
            new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                this._installConfig.ServiceName,
                ServiceProcessInstallerProgressType.Info,
                message)));
    }

    public static UpdateServiceProcessInstaller Create(string serviceName, string displayName, string description, string filePath)
    {
        var serviceInstaller = ServiceProcessInstallerHelper.Create(serviceName, displayName, description, filePath);
        return new UpdateServiceProcessInstaller(serviceInstaller);
    }

    private async Task ExtractPackageToInstallDirectoryAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                ZipFile.ExtractToDirectory(_stream, _installConfig.InstallPath, true);
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"解压文件到{_installConfig.InstallPath}时发生了错误:{ex}";
                RaiseFailedEvent(errorMessage);
                Cancel();
            }
            return false;
        });

    }


    public Task RunAsync()
    {
        return Task.Run(RunInstallLoopImpl);
    }

    private void RunInstallLoopImpl()
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
                        return;
                    case InstallState.Rollback:
                        RollBackImpl(state);
                        return;
                    case InstallState.Error:
                        return;
                    default:
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            RollBackImpl(state);
        }
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
        DetachEvents();
        this._serviceProcessInstaller.Dispose();
    }
}
