
using FluentFTP;
using NodeService.Daemon.Helpers;
using NodeService.Daemon.Models;
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace NodeService.Daemon.Workers
{
    public class UpdateServerConfigWorker : BackgroundService
    {
        private string _localServerConfigPath;
        private string _configPath;
        private string _pluginPath;
        private int _hashCode;
        private ILogger<UpdateServerConfigWorker> _logger;
        private List<Process> _pluginProcesses;
        private string exitId;
        private Mutex _mutexExit;
        private bool _isUpdating;
        FtpServerConfig currentFtpServerConfig;

        public UpdateServerConfigWorker(ILogger<UpdateServerConfigWorker> logger)
        {
            _logger = logger;
            _pluginProcesses = new List<Process>();
            exitId = GetNextExitId();
            _mutexExit = new Mutex(true, exitId);
        }

        public string GetNextExitId()
        {
            return "Global\\" + Guid.NewGuid().ToString();
        }

        private void MyLog(string message)
        {
            _logger.LogInformation(message);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"Start {nameof(UpdateServerConfigWorker)}");
                await Task.Delay(TimeSpan.FromSeconds(10));
                _localServerConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "server.bat");
                _configPath = Path.Combine(AppContext.BaseDirectory, "config");
                _pluginPath = Path.Combine(AppContext.BaseDirectory, "plugins");
                KillAllPluginProcessesByExe();
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        UpdateImpl(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        MyLog(ex.ToString());
                    }
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    int sleepSeconds = 30;
                    if (currentFtpServerConfig != null)
                    {
                        sleepSeconds = Math.Max(sleepSeconds, currentFtpServerConfig.SleepSeconds);
                    }
                    var delay = Math.Min(sleepSeconds, Random.Shared.NextInt64(60 * 3));
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                    _logger.LogInformation($"[HeartBeat]{delay}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            try
            {
                _mutexExit.ReleaseMutex();
                _mutexExit.Dispose();
                KillAllPluginProcessesByExe();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private void KillAllPluginProcessesByExe()
        {
            try
            {
                KillAllPluginProcessesByExeImpl();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private void KillAllPluginProcessesByExeImpl()
        {
            if (!Directory.Exists(_pluginPath))
            {
                return;
            }
            foreach (var item in Directory.GetFiles(_pluginPath, "*.exe", new EnumerationOptions() { RecurseSubdirectories = true }))
            {
                try
                {
                    var fileInfo = new FileInfo(item);
                    var fileNameWithoutEx = Path.GetFileNameWithoutExtension(fileInfo.FullName);
                    var processes = Process.GetProcessesByName(fileNameWithoutEx);
                    foreach (var process in processes)
                    {
                        try
                        {
                            FileInfo targetFileInfo = new FileInfo(process.MainModule.FileName);
                            if (targetFileInfo.FullName == fileInfo.FullName)
                            {
                                process.Kill();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.ToString());
                        }

                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }
        }

        private void UpdateImpl(CancellationToken cancellationToken)
        {
            try
            {
                var loadConfigResult = FtpServerConfig.TryLoadServerConfig(
                                        _localServerConfigPath,
                                        _logger,
                                        out var localFtpServerConfig);

                if (!loadConfigResult)
                {
                    MyLog($"load local server config fail");
                    return;
                }

                currentFtpServerConfig = localFtpServerConfig;

                var server = localFtpServerConfig.server;
                var username = localFtpServerConfig.username;
                var password = localFtpServerConfig.password;

                using var ftpClient = new FtpClient(server, username, password);

                var remoteServerConfigPath = $"JobsWorkerDaemonService/{Dns.GetHostName()}/config/server.bat";
                ftpClient.AutoConnect();
                if (!ftpClient.FileExists(remoteServerConfigPath))
                {
                    MyLog($"remoteServerConfigPath {remoteServerConfigPath} does not exits");
                    return;
                }
                using var memoryStream = new MemoryStream();
                memoryStream.Position = 0;
                if (!ftpClient.DownloadStream(memoryStream, remoteServerConfigPath, 0, LogProgress))
                {
                    MyLog($"Download config {remoteServerConfigPath} fail");
                    return;
                }

                memoryStream.Position = 0;
                var remoteFtpServerConfig = JsonSerializer.Deserialize<FtpServerConfig>(memoryStream);

                if (remoteFtpServerConfig == null)
                {
                    MyLog($"remoteFtpServerConfig null");
                    return;
                }

                if (!TestRemoteConfig(remoteFtpServerConfig))
                {
                    MyLog($"[UpdateImpl]Could not connect remote config " +
                        $"server:{remoteFtpServerConfig.server}" +
                        $"username:{remoteFtpServerConfig.username}" +
                        $"Password:{remoteFtpServerConfig.password}");
                    return;
                }

                if (remoteFtpServerConfig.AsJsonString() == localFtpServerConfig.AsJsonString())
                {
                    MyLog($"[UpdateImpl]dot not need update config");
                    return;
                }

                int updateCount = 0;


                KillAllPluginProcessesByExe();

                updateCount = UpdateConfigFiles(ftpClient, remoteFtpServerConfig, updateCount);
                updateCount = UpdatePluginFiles(ftpClient, remoteFtpServerConfig, updateCount, cancellationToken);


                if (updateCount > 0)
                {
                    PathLocker.Lock(_localServerConfigPath, () =>
                    {

                        File.Delete(_localServerConfigPath);
                        MyLog($"DeleteConfig:{_localServerConfigPath} {localFtpServerConfig.version}");
                        using (var fileStream = File.OpenWrite(_localServerConfigPath))
                        {
                            memoryStream.Position = 0;
                            memoryStream.CopyTo(fileStream);
                        }
                        MyLog($"WriteConfig:{_localServerConfigPath} {remoteFtpServerConfig.version}");
                    });
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    StartPluginProcesses(remoteFtpServerConfig, false);
                    currentFtpServerConfig = remoteFtpServerConfig;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                CheckPluginProcesses(currentFtpServerConfig);
            }
        }

        private bool TestRemoteConfig(FtpServerConfig remoteFtpServerConfig)
        {
            try
            {
                var server = remoteFtpServerConfig.server;
                var username = remoteFtpServerConfig.username;
                var password = remoteFtpServerConfig.password;
                using var ftpClient = new FtpClient(server, username, password);
                var remoteServerConfigPath = $"JobsWorkerDaemonService/{Dns.GetHostName()}/config/server.bat";
                ftpClient.AutoConnect();
                if (!ftpClient.FileExists(remoteServerConfigPath))
                {
                    MyLog($"remoteServerConfigPath {remoteServerConfigPath} does not exits");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        private int UpdatePluginFiles(FtpClient ftpClient, FtpServerConfig remoteFtpServerConfig, int updateCount, CancellationToken cancellationToken)
        {
            try
            {
                var localPluginDirectory = _pluginPath;
                if (!Directory.Exists(localPluginDirectory))
                {
                    Directory.CreateDirectory(localPluginDirectory);
                }
                string tempDirectory = EnsureTempDirectory();
                var remotePluginRootDir = $"JobsWorkerPlugins";

                foreach (var pluginInfo in remoteFtpServerConfig.plugins)
                {
                    KillPluginProcessByProcessName(pluginInfo.name, cancellationToken);
                    for (int i = 0; i < 10; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        if (!DownloadPlugin(ftpClient, pluginInfo, localPluginDirectory, remotePluginRootDir, tempDirectory))
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(10));
                            _logger.LogError($"Download fail:{pluginInfo.name}");
                            continue;
                        }
                        _logger.LogInformation($"Update plugin{pluginInfo.name} success");
                        updateCount++;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }


            return updateCount;

        }

        private void KillPluginProcessByProcessName(string processName, CancellationToken cancellationToken)
        {
            do
            {

                try
                {
                    _logger.LogInformation($"[KillPluginProcess] try kill process:{processName}");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    KillAllPluginProcessesByExe();

                    if (_mutexExit.SafeWaitHandle.IsInvalid)
                    {
                        _mutexExit.ReleaseMutex();
                        _logger.LogInformation($"[KillPluginProcess]release mutex Name:{exitId} Handle {_mutexExit.SafeWaitHandle.DangerousGetHandle()}");
                    }
                    _mutexExit.Dispose();
                    exitId = GetNextExitId();
                    _mutexExit = new Mutex(false, exitId);

                    var processes = Process.GetProcessesByName(processName);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (processes.Length == 0)
                    {
                        _logger.LogInformation($"[KillPluginProcess] no plugin process:{processName}");
                        break;
                    }

                    foreach (var process in processes)
                    {
                        try
                        {
                            process.Kill(true);
                            _logger.LogInformation($"[KillPluginProcess] kill ProcessName:{process.ProcessName} processId:{process.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.ToString());
                        }
                    }
                    KillMyProcesses(processName);
                }
                catch (Exception ex)
                {

                    _logger.LogError(ex.ToString());
                }
                finally
                {
                    Thread.Sleep(10000);
                }


            } while (true);

        }

        private void KillMyProcesses(string processName)
        {
            foreach (var pluginProcess in _pluginProcesses)
            {
                try
                {
                    if (pluginProcess.ProcessName == processName
                        ||
                        string.Compare(pluginProcess.ProcessName, processName, true) == 0
                        ||
                        string.Equals(pluginProcess.ProcessName, processName, StringComparison.OrdinalIgnoreCase)
                        )
                    {
                        pluginProcess.Kill();
                        _logger.LogInformation($"[KillMyProcesses]{pluginProcess.Id}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }
            _pluginProcesses.Clear();
        }

        private void CheckPluginProcesses(FtpServerConfig ftpServerConfig)
        {
            _logger.LogInformation("CheckPluginProcesses");
            if (currentFtpServerConfig != null)
            {
                StartPluginProcesses(ftpServerConfig, true);
            }
        }

        private void StartPluginProcesses(FtpServerConfig ftpServerConfig, bool skipIfExits)
        {
            try
            {
                if (ftpServerConfig.plugins == null)
                {
                    return;
                }
                foreach (var pluginInfo in ftpServerConfig.plugins)
                {
                    try
                    {
                        var pluginWorkerDir = Path.Combine(_pluginPath, pluginInfo.name, pluginInfo.version);
                        var exePath = Path.Combine(_pluginPath, pluginInfo.name, pluginInfo.version, pluginInfo.exePath);
                        if (!File.Exists(exePath))
                        {
                            _logger.LogError($"[Plugin]Could not found plugin {pluginInfo.name} exe:{exePath}");
                            continue;
                        }

                        if (skipIfExits)
                        {
                            var processes = Process.GetProcessesByName(pluginInfo.name);

                            if (processes.Any())
                            {
                                _logger.LogError($"[Plugin]skip create {pluginInfo.name} exe:{exePath}");
                                continue;
                            }
                        }
                        string cmdArgs = $"--exitid {exitId} --parentprocessid {Process.GetCurrentProcess().Id}";
                        Process childProcess = null;
                        if (!ProcessHelper.StartProcessAsCurrentUser(exePath, _logger, out uint processId, cmdArgs, pluginWorkerDir, false))
                        {
                            _logger.LogError($"[Plugin]Could not launch plugin {pluginInfo.name} exe:{exePath} args:{cmdArgs}");
                            childProcess = Process.Start(exePath, cmdArgs);
                            _logger.LogInformation($"Start process:{exePath} args:{cmdArgs}");
                        }
                        else
                        {
                            childProcess = Process.GetProcessById((int)processId);
                        }
                        _pluginProcesses.Add(childProcess);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private string EnsureTempDirectory()
        {
            var tempPluginDirectory = Path.Combine(AppContext.BaseDirectory, "temp");
            if (!Directory.Exists(tempPluginDirectory))
            {
                Directory.CreateDirectory(tempPluginDirectory);
            }
            else
            {
                CleanupFiles(tempPluginDirectory);
            }
            return tempPluginDirectory;
        }

        private bool DownloadPlugin(FtpClient ftpClient,
            PluginInfo pluginInfo,
            string localPluginDirectory,
            string remotePluginRootDir,
            string tempPluginDirectory)
        {
            try
            {
                var localPluginDir = Path.Combine(localPluginDirectory, pluginInfo.name, pluginInfo.version);

                if (!Directory.Exists(localPluginDir))
                {
                    Directory.CreateDirectory(localPluginDir);
                }
                else
                {
                    CleanupFiles(localPluginDir);
                }
                var downloadUrl = Path.Combine(remotePluginRootDir, pluginInfo.name, pluginInfo.version, pluginInfo.filename);
                if (!ftpClient.FileExists(downloadUrl))
                {
                    _logger.LogError($"[FTP]could not download plugin {downloadUrl}");
                    return false;
                }
                var tempFileName = Path.Combine(tempPluginDirectory, Guid.NewGuid().ToString());
                if (File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                }
                using (var fileStream = File.OpenWrite(tempFileName))
                {
                    fileStream.Position = 0;
                    if (!ftpClient.DownloadStream(fileStream, downloadUrl))
                    {
                        _logger.LogError($"[FTP]could not download plugin {downloadUrl}");
                        return false;
                    }
                }
                ZipFile.ExtractToDirectory(tempFileName, localPluginDir, true);
                File.Delete(tempFileName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            return false;
        }

        private void CleanupFiles(string tempPluginDirectory)
        {
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(tempPluginDirectory))
                {
                    try
                    {
                        if (File.Exists(entry))
                        {
                            File.Delete(entry);
                        }
                        else if (Directory.Exists(entry))
                        {
                            Directory.Delete(entry, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private int UpdateConfigFiles(FtpClient ftpClient, FtpServerConfig? remoteFtpServerConfig, int updateCount)
        {
            var localConfigDirectory = Path.Combine(_configPath, remoteFtpServerConfig.version);
            if (!Directory.Exists(localConfigDirectory))
            {
                Directory.CreateDirectory(localConfigDirectory);
            }
            var remoteConfigDirectory = $"JobsWorkerDaemonService/{Dns.GetHostName()}/config/{remoteFtpServerConfig.version}";
            foreach (var configFileName in remoteFtpServerConfig.configfiles)
            {
                var localConfigFilePath = Path.Combine(localConfigDirectory, configFileName);
                var remoteConfigFilePath = Path.Combine(remoteConfigDirectory, configFileName);
                bool remoteExists = ftpClient.FileExists(remoteConfigFilePath);
                bool localExists = File.Exists(localConfigFilePath);
                if (remoteExists && !localExists)
                {
                    if (UpdateConfigFilesImpl(ftpClient,
                        remoteFtpServerConfig,
                        localConfigDirectory,
                        remoteConfigDirectory,
                        configFileName))
                    {
                        updateCount++;
                    }
                    continue;
                }
                if (!remoteExists && localExists)
                {
                    File.Delete(localConfigFilePath);
                }
                var compareResult = ftpClient.CompareFile(localConfigFilePath, remoteConfigFilePath, FtpCompareOption.Size);
                if (compareResult != FtpCompareResult.Equal)
                {
                    if (UpdateConfigFilesImpl(ftpClient,
                        remoteFtpServerConfig,
                        localConfigDirectory,
                        remoteConfigDirectory,
                        configFileName))
                    {
                        updateCount++;
                    }
                    continue;
                }
            }

            return updateCount;
        }

        private bool UpdateConfigFilesImpl(FtpClient ftpClient,
            FtpServerConfig remoteFtpServerConfig,
            string localConfigDirectory,
            string remoteConfigDirectory,
            string configFileName)
        {
            bool result = false;
            try
            {
                var remoteConfigFilePath = Path.Combine(remoteConfigDirectory, configFileName);
                if (!ftpClient.FileExists(remoteConfigFilePath))
                {
                    MyLog($"configFilePath:{remoteConfigFilePath} not found");
                    return result;
                }
                if (!Directory.Exists(localConfigDirectory))
                {
                    Directory.CreateDirectory(localConfigDirectory);
                }
                var localConfigFilePath = Path.Combine(localConfigDirectory, configFileName);
                PathLocker.Lock(localConfigFilePath, () =>
                {
                    if (ftpClient.DownloadFile(localConfigFilePath, remoteConfigFilePath, FtpLocalExists.Overwrite, FtpVerify.Retry, LogProgress) == FtpStatus.Success)
                    {
                        result = true;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                result = false;
            }

            return result;
        }

        private void LogProgress(FtpProgress ftpProgress)
        {
            if (ftpProgress.Progress == 100)
            {
                _logger.LogInformation($"LocalPath:{ftpProgress.LocalPath}=>RemotePath:{ftpProgress.RemotePath} Progress:{ftpProgress.Progress} TransferSpeed:{ftpProgress.TransferSpeed}");

            }

        }


    }
}
