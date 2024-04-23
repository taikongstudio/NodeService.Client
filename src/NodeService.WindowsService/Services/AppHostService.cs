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
        private ConcurrentDictionary<string,Process> _processDict;
        private readonly Channel<ProcessCommandRequest> _commandChannel;
        private readonly IOptionsMonitor<AppOptions> _appOptionsMonitor;
        private readonly IDisposable? _appOptionsMonitorToken;
        private AppOptions _appOptions;

        private const string AppPackageInstallDirectory = "AppPackages";

        public AppHostService(
            ILogger<AppHostService> logger,
            ServiceOptions serviceOptions,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor,
            IOptionsMonitor<AppOptions> appOptionsMonitor
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

            _commandChannel = Channel.CreateUnbounded<ProcessCommandRequest>();

            this._processDict = new ConcurrentDictionary<string, Process>(StringComparer.OrdinalIgnoreCase);

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
                    foreach (var app in _appOptions.Apps)
                    {
                        await CheckAppPackageAsync(app.Name, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }


                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task StartAppProcessAsync(
            string appName,
            bool startNewProcess,
            CancellationToken stoppingToken = default)
        {
            try
            {
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
                KillAppProcessesAsync(appName, stoppingToken);
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
                if (!this._processDict.TryGetValue(appName, out var process) || process == null)
                {
                    return true;
                }
                process.Refresh();
                return process.HasExited;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                this._processDict.TryRemove(appName, out _);
            }
            return true;
        }

        private async Task KillAppProcessAsync(string appName,CancellationToken cancellationToken)
        {
            try
            {
                if (_processDict == null)
                {
                    return;
                }
                if (!await KillProcessAsync(appName, cancellationToken))
                {
                    return;
                }  
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        }

        private async Task CheckAppPackageAsync(string appName, CancellationToken stoppingToken = default)
        {

            bool createNewProcess = false;
            try
            {

                var rsp = await FetchAppPackageUpdateAsync(appName, stoppingToken);
                if (rsp == default)
                {
                    return;
                }
                _logger.LogInformation($"杀死\"{appName}\"进程");
                if (!KillAppProcessesAsync(appName, stoppingToken))
                {
                    _logger.LogInformation($"杀死\"{appName}\"进程失败");
                    return;
                }
                createNewProcess = InstallAppPackage(rsp);
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

        private bool InstallAppPackage((PackageConfigModel? PackageConfig, Stream? Stream) packageInfo)
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
            if (!WriteAppPackageInfo(packageInfo.PackageConfig))
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

        private bool WriteAppPackageInfo(PackageConfigModel packageConfig)
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

        private async Task<bool> KillProcessAsync(string appName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_processDict.TryGetValue(appName, out var process) || process == null)
                {
                    return true;
                }
                string pipeName = $"{appName}-{process.Id}";
                using var pipeClient = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                // Connect to the pipe or wait until the pipe is available.
                Console.Write($"Attempting to connect to pipe {pipeName}...");
                await pipeClient.ConnectAsync(TimeSpan.FromSeconds(30), cancellationToken);

                Console.WriteLine($"Connected to pipe {pipeName}");
                Console.WriteLine("There are currently {0} pipe server instances open.",
                   pipeClient.NumberOfServerInstances);
                var processCommandReq = new ProcessCommandRequest();
                processCommandReq.CommadType = ProcessCommandType.KillProcess;
                await WriteCommandRequest(pipeClient, processCommandReq, cancellationToken);
                await ReadCommandResponse(pipeClient, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                process.Refresh();
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            finally
            {

            }
            return false;
        }

        private async Task WriteCommandRequest(NamedPipeClientStream pipeClient,
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

        private async Task<ProcessCommandResponse> ReadCommandResponse(
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

        private bool KillAppProcessesAsync(string appName,CancellationToken cancellationToken = default)
        {
            try
            {
                var processes = Process.GetProcessesByName(appName);
                _logger.LogInformation($"查询到{processes.Length}个ServiceHost进程");
                foreach (var process in processes)
                {
                    try
                    {
                        _logger.LogInformation($"准备杀死ServiceHost进程:{process.Id}");
                        process.Kill(true);
                        _logger.LogInformation($"已杀死ServiceHost进程:{process.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }

                    try
                    {
                        _logger.LogInformation($"刷新ServiceHost进程状态:{process.Id}");
                        process.Refresh();
                        if (!process.HasExited)
                        {
                            _logger.LogInformation($"ServiceHost进程仍未退出，继续尝试杀死:{process.Id}");
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
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

        private void RunAppProcess(string appName,string installDirectory, PackageConfigModel packageConfig, CancellationToken stoppingToken = default)
        {
            if (this._processDict.TryGetValue(appName, out var process) && process != null)
            {
                return;
            }
            process = new Process();
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

                _processDict.TryAdd(appName, process);

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
                if (process != null)
                {
                    process.Dispose();
                }
                _processDict.TryRemove(appName, out _);
            }
        }

        private async Task RunAppProcessPipeClientAsync(string appName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_processDict.TryGetValue(appName, out var process) || process == null)
                {
                    return;
                }
                var pipeName = $"{appName}-{process.Id}";
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
                    if (!_commandChannel.Reader.TryRead(out var processCommandRequest))
                    {
                        processCommandRequest = new ProcessCommandRequest();
                        processCommandRequest.CommadType = ProcessCommandType.HeartBeat;
                    }
                    await WriteCommandRequest(pipeClient, processCommandRequest, cancellationToken);
                    await ReadCommandResponse(pipeClient, cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
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
