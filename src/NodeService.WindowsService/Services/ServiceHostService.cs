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

namespace NodeService.WindowsService.Services
{
    public class ServiceHostService : BackgroundService
    {
        private readonly ILogger<ServiceHostService> _logger;
        private readonly ServiceOptions _serviceOptions;
        private ApiService _apiService;
        private readonly IOptionsMonitor<ServerOptions> _serverOptionsMonitor;
        private ServerOptions _serverOptions;
        private IDisposable? _serverOptionsMonitorToken;
        private Process? _serviceHostProcess;
        private readonly Channel<ProcessCommandRequest> _commandChannel;

        public ServiceHostService(
            ILogger<ServiceHostService> logger,
            ServiceOptions serviceOptions,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor
            )
        {
            _logger = logger;
            _serviceOptions = serviceOptions;
            _serverOptionsMonitor = serverOptionsMonitor;
            _serverOptions = _serverOptionsMonitor.CurrentValue;
            OnServerOptionsChanged(_serverOptions);
            _serverOptionsMonitorToken = _serverOptionsMonitor.OnChange(OnServerOptionsChanged);
            _commandChannel = Channel.CreateUnbounded<ProcessCommandRequest>();
        }

        public override void Dispose()
        {
            _serverOptionsMonitorToken?.Dispose();
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteCoreAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task StartServiceHostAsync(
            PackageConfigModel? packageConfig,
            bool startNewProcess,
            CancellationToken stoppingToken = default)
        {
            try
            {
                if (!startNewProcess)
                {
                    if (this._serviceHostProcess != null)
                    {
                        if (!IsServiceProcessExited())
                        {
                            return;
                        }

                        await KillCurrentServiceProcessAsync(stoppingToken);
                    }
                }
                if (packageConfig == null && !TryReadPackageInfo(out packageConfig))
                {
                    return;
                }
                if (packageConfig == null)
                {
                    return;
                }
                if (!TryGetInstallDirectory(packageConfig, out var installDirectory) || installDirectory == null)
                {
                    _logger.LogInformation("获取安装目录失败");
                    return;
                }
                _logger.LogInformation("杀死ServiceHost进程");
                KillServiceHostProcessesAsync(stoppingToken);
                _ = Task.Factory.StartNew(() =>
                {
                    RunServiceHostProcess(installDirectory, packageConfig, stoppingToken);
                }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        }

        private bool IsServiceProcessExited()
        {
            try
            {
                if (this._serviceHostProcess == null)
                {
                    return true;
                }
                this._serviceHostProcess.Refresh();
                return this._serviceHostProcess.HasExited;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return true;
        }

        private async Task KillCurrentServiceProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_serviceHostProcess == null)
                {
                    return;
                }
                if (!await KillProcessAsync(_serviceHostProcess, cancellationToken))
                {
                    _serviceHostProcess.Kill(true);
                }  
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        }

        private async Task ExecuteCoreAsync(CancellationToken stoppingToken = default)
        {
            bool createNewProcess = false;
            try
            {
                var rsp = await FetchLastServiceHostPackage(stoppingToken);
                if (rsp == default)
                {
                    return;
                }
                _logger.LogInformation("杀死ServiceHost进程");
                if (!KillServiceHostProcessesAsync(stoppingToken))
                {
                    return;
                }
                createNewProcess = InstallPackage(rsp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                await StartServiceHostAsync(default, createNewProcess, stoppingToken);
            }
        }

        private bool InstallPackage((PackageConfigModel? PackageConfig, Stream? Stream) rsp)
        {
            if (!TryGetInstallDirectory(rsp.PackageConfig, out var installDirectory) || installDirectory == null)
            {
                return false;
            }
            ZipFile.ExtractToDirectory(rsp.Stream, installDirectory, true);
            rsp.Stream.Dispose();
            if (!WritePackageInfo(rsp.PackageConfig))
            {
                return false;
            }
            return true;
        }

        private async Task<(PackageConfigModel? PackageConfig, Stream? Stream)> FetchLastServiceHostPackage(CancellationToken stoppingToken = default)
        {
            _logger.LogInformation("查询ServiceHost更新");
            var rsp = await _apiService.QueryClientUpdateAsync("NodeService.ServiceHost", stoppingToken);
            if (rsp == null)
            {
                _logger.LogInformation("查询失败");
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
                _logger.LogInformation("查询失败");
                return (default, default);
            }
            var packageConfig = clientUpdateConfig.PackageConfig;
            if (packageConfig == null)
            {
                _logger.LogInformation("查询失败");
                return (default, default);
            }
            _logger.LogInformation($"开始验证包:{JsonSerializer.Serialize(packageConfig)}");
            if (ValidatePackageInfo(packageConfig))
            {
                _logger.LogInformation("验证包结束，无需更新");
                return (default, default);
            }
            _logger.LogInformation("开始下载包");
            var downloadPkgRsp = await _apiService.DownloadPackageAsync(packageConfig, stoppingToken);
            _logger.LogInformation($"下载成功，大小:{downloadPkgRsp.Result?.Length}");
            return (packageConfig, downloadPkgRsp.Result);
        }

        private bool ValidatePackageInfo(PackageConfigModel packageConfig)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(packageConfig, nameof(packageConfig));
                if (!TryGetInstallDirectory(packageConfig, out var installDirectory) || installDirectory == null)
                {
                    return false;
                }
                var installedPackagesDirectory = Path.Combine(AppContext.BaseDirectory, ".package", "InstalledPackages");
                if (!Directory.Exists(installedPackagesDirectory))
                {
                    return false;
                }
                var packageInfoPath = Path.Combine(installedPackagesDirectory, packageConfig.Name);
                var json = File.ReadAllText(packageInfoPath);
                var json2 = JsonSerializer.Serialize(packageConfig);
                return json == json2;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            return false;
        }

        private bool WritePackageInfo(PackageConfigModel packageConfig)
        {
            try
            {
                var installedPackagesDirectory = Path.Combine(AppContext.BaseDirectory, ".package", "InstalledPackages");
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

        private bool TryReadPackageInfo(out PackageConfigModel? packageConfig)
        {
            packageConfig = null;
            try
            {
                var packageInfoPath = Path.Combine(AppContext.BaseDirectory, ".package", "InstalledPackages", "NodeService.ServiceHost");
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

        private async Task<bool> KillProcessAsync(Process? process, CancellationToken cancellationToken = default)
        {
            try
            {
                if (process == null)
                {
                    return true;
                }
                string pipeName = $"NodeService.ServiceHost-{process.Id}";
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
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

        private bool KillServiceHostProcessesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var processes = Process.GetProcessesByName("NodeService.ServiceHost");
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
                path = Path.Combine(AppContext.BaseDirectory, "Packages", packageConfig.Name, packageConfig.Id);
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

        private void RunServiceHostProcess(string installDirectory, PackageConfigModel packageConfig, CancellationToken stoppingToken = default)
        {
            try
            {
                var fileName= Path.Combine(installDirectory, packageConfig.EntryPoint);
                _logger.LogInformation($"启动ServiceHost进程:{fileName}");
                _serviceHostProcess = new Process();
                _serviceHostProcess.StartInfo.FileName = Path.Combine(installDirectory, packageConfig.EntryPoint);
                _serviceHostProcess.StartInfo.Arguments = $"--env {_serviceOptions.env} --pid {Environment.ProcessId}";
                _serviceHostProcess.StartInfo.WorkingDirectory = installDirectory;
                _serviceHostProcess.StartInfo.UseShellExecute = false;
                _serviceHostProcess.StartInfo.CreateNoWindow = true;
                _serviceHostProcess.StartInfo.RedirectStandardError = true;
                _serviceHostProcess.StartInfo.RedirectStandardOutput = true;
                _serviceHostProcess.Start();
                _serviceHostProcess.Exited += ServiceHostProcess_Exited;
                _serviceHostProcess.OutputDataReceived += WriteOutput;
                _serviceHostProcess.ErrorDataReceived += WriteError;
                _serviceHostProcess.BeginOutputReadLine();
                _serviceHostProcess.BeginErrorReadLine();

                int taskIndex = Task.WaitAny(
                    _serviceHostProcess.WaitForExitAsync(),
                    Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken), Task.Run(() => {
                        RunProcessClientAsync(stoppingToken).Wait(stoppingToken);
                    }, stoppingToken));

                _serviceHostProcess.Exited -= ServiceHostProcess_Exited;
                _serviceHostProcess.OutputDataReceived -= WriteOutput;
                _serviceHostProcess.ErrorDataReceived -= WriteError;

                if (taskIndex != 0)
                {
                    _logger.LogInformation($"杀死ServiceHost进程:{fileName}");
                    _serviceHostProcess.Kill(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                if (_serviceHostProcess != null)
                {
                    _serviceHostProcess.Dispose();
                }
            }
        }

        private async Task RunProcessClientAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_serviceHostProcess == null)
                {
                    return;
                }
                var pipeName = $"NodeService.ServiceHost-{_serviceHostProcess.Id}";
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


        private void WriteOutput(object sender, DataReceivedEventArgs e)
        {
            //_logger.LogInformation(e.Data);
        }

        private void WriteError(object sender, DataReceivedEventArgs e)
        {
            //_logger.LogError(e.Data);
        }

        private void ServiceHostProcess_Exited(object? sender, EventArgs e)
        {

        }
    }
}
