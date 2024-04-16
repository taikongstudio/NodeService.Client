
using FluentFTP;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public class WindowsServiceProcessInstaller : IDisposable
{
    private readonly ServiceProcessInstaller _serviceProcessInstaller;
    private const string ServiceName = "NodeService.WindowsService";
    private string _installDirectory;
    private MemoryStream _stream;
    private IProgress<FtpProgress> _progressProvider;

    private enum InstallState
    {
        Uninstall,
        Install,
        Commit,
        Rollback,
    }

    private InstallState _state;

    private CancellationTokenSource _cancellationTokenSource;
    private ApiService _apiService;
    private ClientUpdateConfigModel _clientUpdateConfig;
    private PackageConfigModel _packageConfig;

    public event EventHandler<InstallerProgressEventArgs> Failed;

    public event EventHandler<InstallerProgressEventArgs> ProgressChanged;

    public event EventHandler<InstallerProgressEventArgs> Completed;

    private WindowsServiceProcessInstaller(ServiceProcessInstaller serviceProcessInstaller)
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
        RaiseProgressChangedEvent($"正在清理目录\"{_installDirectory}\"");
        CleanupInstallDirectory();
        RaiseProgressChangedEvent("清理成功");
    }

    private bool CleanupInstallDirectory()
    {
        try
        {
            var installDirectory = new DirectoryInfo(_installDirectory);
            if (!installDirectory.Exists)
            {
                return true;
            }
            installDirectory.Delete(true);
            return true;
        }
        catch (Exception ex)
        {
            string errorMessage = $"清理目录\"{_installDirectory}\"时发生了错误:{ex}";
            RaiseFailedEvent(errorMessage);
            Cancel();
        }
        return false;
    }

    public void SetParameters(
        ApiService apiService,
        ClientUpdateConfigModel clientUpdateConfig,
        string installDirectory)
    {
        _apiService = apiService;
        _clientUpdateConfig = clientUpdateConfig;
        _installDirectory = installDirectory;
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
        RaiseProgressChangedEvent($"正在Kill服务进程\"{ServiceName}\"");
        if (KillServiceProcessFast())
        {
            RaiseProgressChangedEvent($"Kill服务进程成功\"{ServiceName}\"");
        }
        else
        {
            RaiseProgressChangedEvent($"Kill服务进程失败\"{ServiceName}\"");
        }
        RaiseProgressChangedEvent($"正在卸载服务\"{ServiceName}\"");
    }

    private bool KillServiceProcessFast()
    {
        try
        {
            string exitTxtFilePath = Path.Combine(_installDirectory, "exit.txt");
            File.WriteAllText(exitTxtFilePath, string.Empty);
            using var serviceController = new ServiceController(ServiceName);
            serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
            return true;
        }
        catch (Exception ex)
        {

        }
        return false;
    }

    private void _serviceProcessInstaller_AfterInstall(object sender, InstallEventArgs e)
    {
        WaitForServiceRunning().Wait();
    }

    private async Task WaitForServiceRunning()
    {
        try
        {
            using ServiceController serviceController = new ServiceController(ServiceName);
            serviceController.Start();
            RaiseProgressChangedEvent($"等待服务\"{ServiceName}\"运行");
            serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(180));
            RaiseProgressChangedEvent($"服务\"{ServiceName}\"已运行");
            await _apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
            {
                ClientUpdateConfigId = _clientUpdateConfig.Id,
                NodeName = Dns.GetHostName(),
                CategoryName = "StartSuccess"
            });
            RaiseCompletedEvent("安装成功");
        }
        catch (Exception ex)
        {
            string errorMessage = $"启动服务\"{ServiceName}\"时发生了错误:{ex.Message}";
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

            InitStream();
            RaiseProgressChangedEvent($"正在下载：{_clientUpdateConfig.PackageConfig.DownloadUrl}");
            await _apiService.DownloadPackageAsync(_clientUpdateConfig.PackageConfig, _stream);
            RaiseProgressChangedEvent($"Download {_clientUpdateConfig}");
            await _apiService.AddOrUpdateUpdateInstallCounterAsync(new AddOrUpdateCounterParameters()
            {
                ClientUpdateConfigId = _clientUpdateConfig.Id,
                NodeName = Dns.GetHostName(),
                CategoryName = "DownloadSuccess"
            });
            _stream.Position = 0;
            if (!IsZipArchive(_stream))
            {
                this.Cancel();
                return;
            }
            _stream.Position = 0;

            RaiseProgressChangedEvent($"下载成功，大小:{_stream.Length}");

            await ExtractPackageToInstallDirectoryAsync();
        }
        catch (Exception ex)
        {
            RaiseFailedEvent(ex.Message);
            Cancel();
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
            RaiseFailedEvent(ex.Message);
            Cancel();
        }
        finally
        {

        }

        return false;
    }

    public void Cancel()
    {
        if (this._cancellationTokenSource != null)
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
                _installDirectory,
                ServiceProcessInstallerProgressType.Error,
                message)));
    }

    private void RaiseProgressChangedEvent(string message)
    {
        this.ProgressChanged?.Invoke(
            this,
            new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                _installDirectory,
                ServiceProcessInstallerProgressType.Info,
                message)));
    }

    private void RaiseCompletedEvent(string message)
    {
        this.Completed?.Invoke(
            this,
            new InstallerProgressEventArgs(new ServiceProcessInstallerProgress(
                _installDirectory,
                ServiceProcessInstallerProgressType.Info,
                message)));
    }

    public static WindowsServiceProcessInstaller Create(string serviceName, string displayName, string description, string filePath)
    {
        var serviceInstaller = ServiceProcessInstallerHelper.Create(serviceName, displayName, description, filePath);
        return new WindowsServiceProcessInstaller(serviceInstaller);
    }

    private async Task ExtractPackageToInstallDirectoryAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                ZipFile.ExtractToDirectory(_stream, _installDirectory, true);
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"解压文件到{_installDirectory}时发生了错误:{ex}";
                RaiseFailedEvent(errorMessage);
                Cancel();
            }
            return false;
        });

    }


    public Task<bool> RunAsync()
    {
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
                        return true;
                    case InstallState.Rollback:
                        RollBackImpl(state);
                        return false;
                    default:
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            RollBackImpl(state);
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
        DetachEvents();
        this._serviceProcessInstaller.Dispose();
    }
}
