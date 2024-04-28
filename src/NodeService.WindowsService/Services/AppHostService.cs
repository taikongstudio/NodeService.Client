using NodeService.Infrastructure;
using NodeService.Infrastructure.NodeSessions;
using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodeService.Infrastructure.DataModels;
using Microsoft.Extensions.Options;
using NodeService.WindowsService.Models;
using System.Security.Cryptography;
using System.IO.Pipes;
using NodeService.Infrastructure.Models;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using NodeService.Infrastructure.Messages;
using System.Collections.Concurrent;
using System.ServiceProcess;
using System.ComponentModel;
using NodeService.Infrastructure.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using static NodeService.Infrastructure.Services.NodeService;
using System.Net;
using static NodeService.Infrastructure.Services.ProcessService;

namespace NodeService.WindowsService.Services
{
    public class AppHostService : BackgroundService
    {
        private readonly ILogger<AppHostService> _logger;
        private readonly ServiceOptions _serviceOptions;
        private ApiService _apiService;
        private readonly IOptionsMonitor<ServerOptions> _serverOptionsMonitor;
        private ServerOptions _serverOptions;
        private IDisposable? _serverOptionsMonitorToken;
        private readonly ConcurrentDictionary<string, ProcessChannelInfo> _processDictionary;
        private readonly IOptionsMonitor<AppOptions> _appOptionsMonitor;
        private readonly IDisposable? _appOptionsMonitorToken;
        private AppOptions _appOptions;
        private ConcurrentDictionary<string, ProcessServiceClientCache?> _processServiceClientCaches;

        private const string AppPackageInstallDirectory = "AppPackages";

        public AppHostService(
            ILogger<AppHostService> logger,
            ServiceOptions serviceOptions,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor,
            IOptionsMonitor<AppOptions> appOptionsMonitor,
            [FromKeyedServices(Constants.ProcessChannelInfoDictionary)]
            ConcurrentDictionary<string, ProcessChannelInfo> processDictionary
            )
        {
            _logger = logger;
            _serviceOptions = serviceOptions;
            _serverOptionsMonitor = serverOptionsMonitor;
            _serverOptions = _serverOptionsMonitor.CurrentValue;
            OnServerOptionsChanged(_serverOptions);
            _serverOptionsMonitorToken = _serverOptionsMonitor.OnChange(OnServerOptionsChanged);

            _appOptionsMonitor = appOptionsMonitor;
            _appOptions = _appOptionsMonitor.CurrentValue;
            OnAppOptionsChanged(_appOptions);
            _appOptionsMonitorToken = _appOptionsMonitor.OnChange(OnAppOptionsChanged);
            this._processDictionary = processDictionary;
            _processServiceClientCaches = new ConcurrentDictionary<string, ProcessServiceClientCache>();
        }

        public override void Dispose()
        {
            _serverOptionsMonitorToken?.Dispose();
            _appOptionsMonitorToken?.Dispose();
            base.Dispose();
        }

        private void OnServerOptionsChanged(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
            if (this._apiService != null)
            {
                this._apiService.Dispose();
            }
            _apiService = new ApiService(new HttpClient()
            {
                BaseAddress = new Uri(_serverOptions.HttpAddress)
            });
        }

        private void OnAppOptionsChanged(AppOptions appOptions)
        {
            _appOptions = appOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await WaitForServiceNotAvailableAsync(stoppingToken);
                    foreach (var app in _appOptions.Apps)
                    {
                        await CheckAppPackageAsync(app.Name, stoppingToken);
                        await WaitForServiceNotAvailableAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }


                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private bool TryGetProcessServiceClient(
            string serviceName,
            out ProcessServiceClient? processServiceClient,
            CancellationToken cancellationToken = default)
        {
            processServiceClient = null;
            try
            {
                processServiceClient = this._processServiceClientCaches.GetOrAdd(
                    serviceName,
                    (key) => CreateCache(serviceName, cancellationToken))?.ProcessServiceClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return processServiceClient != null;
        }

        private ProcessServiceClientCache? CreateCache(string serviceName, CancellationToken cancellationToken = default)
        {
            return new ProcessServiceClientCache(ProcessServiceClientCache.CreateChannel(serviceName, cancellationToken));
        }

        private async Task WaitForServiceNotAvailableAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            string currentServiceName = $"NodeService.{_serviceOptions.mode}";
            string[] serviceNames = [Constants.ServiceProcessUpdateService, Constants.ServiceProcessWorkerService];
            if (currentServiceName != Constants.ServiceProcessWindowsService)
            {
                await DetectServiceStatusAsync(Constants.ServiceProcessWindowsService, cancellationToken);
                foreach (var serviceName in serviceNames)
                {
                    if (serviceName == currentServiceName)
                    {
                        continue;
                    }
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    await DetectServiceStatusAsync(serviceName, cancellationToken);
                }
            }
            else
            {
                foreach (var serviceName in serviceNames)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    await TrykillServiceAppProcessAsync(currentServiceName, serviceName, cancellationToken);
                }
            }
        }



        private async Task<bool> TrykillServiceAppProcessAsync(
            string sender,
            string serviceName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                _logger.LogInformation($"杀死服务\"{serviceName}\"的应用进程");
                var killProcessRequest = new KillAppProcessRequest()
                {
                    RequestId = Guid.NewGuid().ToString(),

                };
                killProcessRequest.Parameters.Add("Sender", sender);

                if (!this.TryGetProcessServiceClient(
                    serviceName,
                    out var processServiceClient,
                    cancellationToken) || processServiceClient == null)
                {
                    return true;
                }
                _logger.LogInformation("Send KillProcessRequest");
                await processServiceClient.KillAppProcessesAsync(
                    killProcessRequest,
                    cancellationToken: cancellationToken);
                _logger.LogInformation("KillProcessResponse");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return false;
        }

        private async Task DetectServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                _logger.LogInformation($"检查服务\"{serviceName}\"的状态");
                using var serviceController = new ServiceController(serviceName);
                while (serviceController.Status == ServiceControllerStatus.Running)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    if (!TryGetProcessServiceClient(
                        serviceName,
                        out var processServiceClient,
                        cancellationToken
                        ) || processServiceClient == null)
                    {
                        return;
                    }
                    QueryAppProcessRequest queryAppProcessRequest = new QueryAppProcessRequest()
                    {
                        RequestId = Guid.NewGuid().ToString()
                    };
                    _logger.LogInformation($"查询服务\"{serviceName}\"的应用进程");
                    var queryAppProcessesResponse = await processServiceClient.QueryAppProcessesAsync(
                        queryAppProcessRequest,
                        cancellationToken: cancellationToken);
                    _logger.LogInformation($"查询服务\"{serviceName}\"的应用进程成功，共{queryAppProcessesResponse.AppProcesses.Count}个进程");
                    foreach (var item in queryAppProcessesResponse.AppProcesses)
                    {
                        _logger.LogInformation($"查询服务\"{serviceName}\"的应用进程：PID：{item.Id} 名称：{item.Name} 是否退出：{item.HasExited}");
                    }

                    if (queryAppProcessesResponse.AppProcesses.Count > 0)
                    {
                        foreach (var app in _appOptions.Apps)
                        {
                            await KillAppProcessAsync(app.Name, cancellationToken);
                        }
                    }
                    else
                    {
                        break;
                    }
                    serviceController.Refresh();
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is Win32Exception win32Exception && win32Exception.NativeErrorCode == 1060)
                {
                    _logger.LogInformation(ex.Message);
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
        }

        private async Task StartAppProcessAsync(
            string appName,
            bool startNewProcess,
            CancellationToken stoppingToken = default)
        {
            try
            {
                _logger.LogInformation("启动应用");
                if (!startNewProcess)
                {
                    if (!IsAppProcessExited(appName))
                    {
                        return;
                    }

                    await KillAppProcessAsync(appName, stoppingToken);
                }
                if (!TryReadAppPackageInfo(appName,out var packageConfig))
                {
                    _logger.LogInformation("获取应用包信息失败");
                    return;
                }
                if (packageConfig == null)
                {
                    _logger.LogInformation("获取应用包信息失败");
                    return;
                }
                if (!TryGetInstallDirectory(packageConfig, out var installDirectory) || installDirectory == null)
                {
                    _logger.LogInformation("获取安装目录失败");
                    return;
                }
                _logger.LogInformation("杀死ServiceHost进程");
                await KillAppProcessAsync(appName, stoppingToken);
                _ = Task.Factory.StartNew(() =>
                {
                    RunAppProcess(appName, installDirectory, packageConfig, stoppingToken);
                }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        }

        private bool IsAppProcessExited(string appName)
        {
            try
            {
                if (!this._processDictionary.TryGetValue(appName, out var processChannelInfo) || processChannelInfo == null)
                {
                    return true;
                }
                processChannelInfo.Process.Refresh();
                if (processChannelInfo.Process.HasExited)
                {
                    this._processDictionary.TryRemove(appName, out _);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                this._processDictionary.TryRemove(appName, out _);
            }
            return true;
        }

        private async Task<bool> KillAppProcessAsync(string appName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await KillProcessByPipeClientAsync(appName, cancellationToken))
                {
                    return KillAppProcessByKillAsync(appName, cancellationToken);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return false;
        }

        private async Task CheckAppPackageAsync(string appName, CancellationToken stoppingToken = default)
        {

            bool createNewProcess = false;
            try
            {

                var packageInfo = await FetchAppPackageUpdateAsync(appName, stoppingToken);
                if (packageInfo == default)
                {
                    return;
                }
                _logger.LogInformation($"杀死\"{appName}\"进程");
                if (!await KillAppProcessAsync(appName, stoppingToken))
                {
                    _logger.LogInformation($"杀死\"{appName}\"进程失败");
                    return;
                }
                createNewProcess = InstallAppPackage(packageInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            finally
            {
                await StartAppProcessAsync(appName, createNewProcess, stoppingToken);
            }
        }

        private bool InstallAppPackage((PackageConfigModel PackageConfig, Stream Stream) packageInfo)
        {
            if (packageInfo.PackageConfig == null || packageInfo.Stream == null)
            {
                return false;
            }
            if (!TryGetInstallDirectory(packageInfo.PackageConfig, out var installDirectory) || installDirectory == null)
            {
                _logger.LogInformation("获取安装目录失败");
                return false;
            }
            _logger.LogInformation($"解压安装包到:{installDirectory}");
            ZipFile.ExtractToDirectory(packageInfo.Stream, installDirectory, true);
            packageInfo.Stream.Dispose();
            if (!TryWriteAppPackageInfo(packageInfo.PackageConfig))
            {
                _logger.LogInformation("写入包信息失败");
                return false;
            }
            _logger.LogInformation("写入包信息成功");
            return true;
        }

        private async Task<(PackageConfigModel? PackageConfig, Stream? Stream)> FetchAppPackageUpdateAsync(string appName,CancellationToken stoppingToken = default)
        {
            _logger.LogInformation($"查询应用\"{appName}\"更新");
            var rsp = await _apiService.QueryClientUpdateAsync(appName, stoppingToken);
            if (rsp == null)
            {
                _logger.LogInformation($"查询应用\"{appName}\"失败");
                return (default, default);
            }
            if (rsp.ErrorCode != 0)
            {
                _logger.LogInformation(rsp.Message);
                return (default, default);
            }
            var clientUpdateConfig = rsp.Result;
            if (clientUpdateConfig == null)
            {
                _logger.LogInformation($"查询应用\"{appName}\"失败");
                return (default, default);
            }
            var packageConfig = clientUpdateConfig.PackageConfig;
            if (packageConfig == null)
            {
                _logger.LogInformation($"查询应用\"{appName}\"失败");
                return (default, default);
            }
            _logger.LogInformation($"开始验证包:{JsonSerializer.Serialize(packageConfig)}");
            if (TryValidateAppPackageInfo(packageConfig))
            {
                _logger.LogInformation("验证包结束，无需更新");
                return (default, default);
            }
            _logger.LogInformation("开始下载包");
            var downloadPkgRsp = await _apiService.DownloadPackageAsync(packageConfig, stoppingToken);
            _logger.LogInformation($"下载成功，大小:{downloadPkgRsp.Result?.Length}");
            return (packageConfig, downloadPkgRsp.Result);
        }

        private bool TryValidateAppPackageInfo(PackageConfigModel packageConfig)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(packageConfig, nameof(packageConfig));
                if (!TryGetInstallDirectory(packageConfig, out var installDirectory) || installDirectory == null)
                {
                    return false;
                }
                var installedPackagesDirectory = Path.Combine(AppContext.BaseDirectory, ".package", AppPackageInstallDirectory);
                if (!Directory.Exists(installedPackagesDirectory))
                {
                    return false;
                }
                var packageInfoPath = Path.Combine(installedPackagesDirectory, packageConfig.Name);
                var json = File.ReadAllText(packageInfoPath);
                var json2 = JsonSerializer.Serialize(packageConfig);
                var entryPoint = Path.Combine(installDirectory, packageConfig.EntryPoint);
                return json == json2 && File.Exists(entryPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            return false;
        }

        private bool TryWriteAppPackageInfo(PackageConfigModel packageConfig)
        {
            try
            {
                var installedPackagesDirectory = Path.Combine(AppContext.BaseDirectory, ".package", AppPackageInstallDirectory);
                if (!Directory.Exists(installedPackagesDirectory))
                {
                    Directory.CreateDirectory(installedPackagesDirectory);
                }
                var infoPath = Path.Combine(installedPackagesDirectory, packageConfig.Name);
                File.WriteAllText(infoPath, JsonSerializer.Serialize(packageConfig));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return false;
        }

        private bool TryReadAppPackageInfo(string serviceAppName,out PackageConfigModel? packageConfig)
        {
            packageConfig = null;
            try
            {
                var packageInfoPath = Path.Combine(AppContext.BaseDirectory, ".package", AppPackageInstallDirectory, serviceAppName);
                if (File.Exists(packageInfoPath))
                {
                    packageConfig = JsonSerializer.Deserialize<PackageConfigModel>(File.ReadAllText(packageInfoPath));
                    return packageConfig != null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            return false;
        }

        private async Task<bool> KillProcessByPipeClientAsync(string appName, CancellationToken cancellationToken = default)
        {
            if (!_processDictionary.TryGetValue(appName, out var processChannelInfo) || processChannelInfo == null)
            {
                return true;
            }
            try
            {
                await processChannelInfo.ProcessCommandChannel.Writer.WriteAsync(new ProcessCommandRequest()
                {
                    CommadType = ProcessCommandType.KillProcess,
                }, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                processChannelInfo.Process.Refresh();
                if (processChannelInfo.Process.HasExited)
                {
                    _processDictionary.TryRemove(appName, out _);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                _processDictionary.TryRemove(appName, out _);
            }
            finally
            {
                processChannelInfo.Process.Dispose();
            }
            return false;

        }

        async Task WriteCommandRequest(NamedPipeClientStream pipeClient,
            ProcessCommandRequest req,
            CancellationToken cancellationToken = default)
        {
            using var streamWriter = new StreamWriter(pipeClient, leaveOpen: true);
            streamWriter.AutoFlush = true;
            var jsonString = JsonSerializer.Serialize(req);
            if (Debugger.IsAttached)
            {
                _logger.LogInformation($"Client send req:{jsonString}.");
            }
            await streamWriter.WriteAsync(jsonString);
            await streamWriter.WriteLineAsync();
        }

        async Task<ProcessCommandResponse> ReadCommandResponse(
            NamedPipeClientStream pipeClient,
            CancellationToken cancellationToken = default)
        {
            using var streamReader = new StreamReader(pipeClient, leaveOpen: true);
            var jsonString = await streamReader.ReadLineAsync(cancellationToken);
            if (Debugger.IsAttached)
            {
                _logger.LogInformation($"Client recieve rsp:{jsonString}.");
            }
            var rsp = JsonSerializer.Deserialize<ProcessCommandResponse>(jsonString);
            return rsp;
        }


        private bool KillAppProcessByKillAsync(string appName, CancellationToken cancellationToken = default)
        {
            try
            {

                if (!this._processDictionary.TryGetValue(appName, out var processChannelInfo) || processChannelInfo == null)
                {
                    _logger.LogInformation($"没有启动的{appName}进程");
                    return true;
                }
                try
                {
                    _logger.LogInformation($"准备杀死{appName}进程：{processChannelInfo.Process.Id}");
                    processChannelInfo.Process.Kill(true);
                    _logger.LogInformation($"已杀死{appName}进程：{processChannelInfo.Process.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

                try
                {
                    _logger.LogInformation($"刷新{appName}进程状态：{processChannelInfo.Process.Id}");
                    processChannelInfo.Process.Refresh();
                    if (!processChannelInfo.Process.HasExited)
                    {
                        _logger.LogInformation($"{appName}进程：{processChannelInfo.Process.Id}仍未退出，继续尝试杀死。");
                        processChannelInfo.Process.Kill();
                        _logger.LogInformation($"{appName}进程：{processChannelInfo.Process.Id}已执行杀死操作");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
                finally
                {
                    processChannelInfo.Process.Dispose();
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                this._processDictionary.TryRemove(appName, out _);
            }
            return false;
        }

        private bool TryGetInstallDirectory(
            PackageConfigModel packageConfig,
            out string? path,
            bool createIfNotExits = true
            )
        {
            path = null;
            try
            {
                path = Path.Combine(AppContext.BaseDirectory, AppPackageInstallDirectory, packageConfig.Name, packageConfig.Id);
                if (!Directory.Exists(path))
                {
                    if (!createIfNotExits)
                    {
                        return false;
                    }
                    Directory.CreateDirectory(path);
                }
                return path != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return false;
        }

        private void RunAppProcess(
            string appName,
            string installDirectory,
            PackageConfigModel packageConfig,
            CancellationToken stoppingToken = default)
        {
            if (this._processDictionary.TryGetValue(appName, out var processChannelInfo) && processChannelInfo != null)
            {
                return;
            }
            using var process = new Process();
            try
            {
                var fileName= Path.Combine(installDirectory, packageConfig.EntryPoint);
                _logger.LogInformation($"启动进程:{fileName}");
             
                process.StartInfo.FileName = Path.Combine(installDirectory, packageConfig.EntryPoint);
                process.StartInfo.Arguments = $"--env {_serviceOptions.env} --pid {Environment.ProcessId}";
                process.StartInfo.WorkingDirectory = installDirectory;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                process.Exited += AppProcess_Exited;
                process.OutputDataReceived += AppProcessWriteOutput;
                process.ErrorDataReceived += AppProcessWriteError;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _processDictionary.TryAdd(appName, new ProcessChannelInfo(process));

                int taskIndex = Task.WaitAny(
                    process.WaitForExitAsync(),
                    Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken), Task.Run(() => {
                        RunAppProcessPipeClientAsync(appName, stoppingToken).Wait(stoppingToken);
                    }, stoppingToken));

                process.Exited -= AppProcess_Exited;
                process.OutputDataReceived -= AppProcessWriteOutput;
                process.ErrorDataReceived -= AppProcessWriteError;

                if (taskIndex != 0)
                {
                    _logger.LogInformation($"杀死应用进程:{fileName}");
                    process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                _processDictionary.TryRemove(appName, out _);
            }
        }

        private async Task RunAppProcessPipeClientAsync(
            string appName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_processDictionary.TryGetValue(appName, out var processChannelInfo) || processChannelInfo == null)
                {
                    return;
                }
                var pipeName = $"{appName}-{processChannelInfo.Process.Id}";
                using var pipeClient = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut);
                // Connect to the pipe or wait until the pipe is available.
                _logger.LogInformation($"Attempting to connect to pipe {pipeName}...");
                await pipeClient.ConnectAsync(TimeSpan.FromSeconds(30), cancellationToken);

                _logger.LogInformation($"Connected to pipe {pipeName}");
                _logger.LogInformation($"There are currently {pipeClient.NumberOfServerInstances} pipe server instances open.");

                while (pipeClient.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    if (!processChannelInfo.ProcessCommandChannel.Reader.TryPeek(out var processCommandRequest))
                    {

                        processCommandRequest = new ProcessCommandRequest();
                        processCommandRequest.CommadType = ProcessCommandType.HeartBeat;
                    }
                    await WriteCommandRequest(pipeClient, processCommandRequest, cancellationToken);
                    await ReadCommandResponse(pipeClient, cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }


        private void AppProcessWriteOutput(object sender, DataReceivedEventArgs e)
        {
            //_logger.LogInformation(e.Data);
        }

        private void AppProcessWriteError(object sender, DataReceivedEventArgs e)
        {
            //_logger.LogError(e.Data);
        }

        private void AppProcess_Exited(object? sender, EventArgs e)
        {

        }
    }
}
