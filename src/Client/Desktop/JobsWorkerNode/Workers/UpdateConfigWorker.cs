
using FluentFTP;
using JobsWorkerNode.Helpers;
using JobsWorkerNode.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace JobsWorkerNode.Workers
{
    public class UpdateConfigWorker : BackgroundService
    {
        private string _localServerConfigPath;
        private string _configPath;
        private string _remoteServerConfigRoot;
        private int _hashCode;
        private ILogger<UpdateConfigWorker> _logger;
        private List<Process> _pluginProcesses;
        private bool _isUpdating;
        FtpServerConfig currentFtpServerConfig;
        private string? _pluginPath;

        public UpdateConfigWorker(ILogger<UpdateConfigWorker> logger)
        {
            _logger = logger;
        }

        private void MyLog(string message)
        {
            _logger.LogInformation(message);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($"Start {nameof(UpdateConfigWorker)}");
                await Task.Delay(TimeSpan.FromSeconds(10));
                _localServerConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "server.bat");
                _configPath = Path.Combine(AppContext.BaseDirectory, "config");
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
                    var delay = Debugger.IsAttached ? 10 : Math.Max(sleepSeconds * Random.Shared.NextDouble(), Random.Shared.NextInt64(60 * 3));
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                    _logger.LogInformation($"[HeartBeat]{delay}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
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

                if (!loadConfigResult && currentFtpServerConfig == null)
                {
                    MyLog($"load local server config fail");
                    return;
                }

                currentFtpServerConfig = localFtpServerConfig;

                var server = localFtpServerConfig.server;
                var username = localFtpServerConfig.username;
                var password = localFtpServerConfig.password;

                using var ftpClient = new FtpClient(server, username, password);
                ftpClient.AutoConnect();
                _remoteServerConfigRoot = $"/JobsWorkerDaemonService/{Dns.GetHostName()}/plugins/JobsWorker/config";
                _pluginPath = Path.Combine(AppContext.BaseDirectory, "plugins");
                var remoteServerConfigPath = Path.Combine(_remoteServerConfigRoot, "server.bat");

                if (!ftpClient.DirectoryExists(_remoteServerConfigRoot))
                {
                    ftpClient.CreateDirectory(_remoteServerConfigRoot);
                }
                if (!ftpClient.FileExists(remoteServerConfigPath))
                {
                    MyLog($"remote plugin config path {remoteServerConfigPath} does not exits");
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

                if (ShouldTestRemoteCondig(currentFtpServerConfig, remoteFtpServerConfig) && !TestRemoteConfig(remoteFtpServerConfig))
                {
                    MyLog($"[UpdateImpl]Could not connect remote config " +
                        $"server:{remoteFtpServerConfig.server}" +
                        $"username:{remoteFtpServerConfig.username}" +
                        $"Password:{remoteFtpServerConfig.password}");
                    return;
                }

                if (remoteFtpServerConfig.AsJsonString() == localFtpServerConfig.AsJsonString() && ValidateConfig(remoteFtpServerConfig))
                {
                    MyLog($"[UpdateImpl]dot not need update config");
                    return;
                }

                int updateCount = 0;

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
                    currentFtpServerConfig = remoteFtpServerConfig;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    StartPluginProcesses(currentFtpServerConfig, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {

            }
        }

        private bool ValidateConfig(FtpServerConfig ftpServerConfig)
        {
            if (ftpServerConfig.configfiles == null || ftpServerConfig.configfiles.Length == 0)
            {
                return true;
            }
            var localConfigDirectory = Path.Combine(_configPath, ftpServerConfig.version);
            foreach (var configFileName in ftpServerConfig.configfiles)
            {
                var localConfigFilePath = Path.Combine(localConfigDirectory, configFileName);
                if (!File.Exists(localConfigFilePath))
                {
                    _logger.LogError($"missing file:{localConfigFilePath}");
                    return false;
                }
            }
            if (ftpServerConfig.plugins != null)
            {
                foreach (var pluginInfo in ftpServerConfig.plugins)
                {
                    var localPluginDir = Path.Combine(_pluginPath, pluginInfo.name, pluginInfo.version);
                    if (!Directory.Exists(localPluginDir))
                    {
                        _logger.LogError($"missing directory:{localPluginDir}");
                        return false;
                    }
                    var entryPoint = Path.Combine(localPluginDir, pluginInfo.exePath);
                    if (!File.Exists(entryPoint))
                    {
                        _logger.LogError($"missing entryPoint:{entryPoint}");
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ShouldTestRemoteCondig(FtpServerConfig currentFtpServerConfig, FtpServerConfig remoteFtpServerConfig)
        {
            return currentFtpServerConfig.server != remoteFtpServerConfig.server
                ||
                currentFtpServerConfig.username != remoteFtpServerConfig.username
                ||
                currentFtpServerConfig.password != remoteFtpServerConfig.password;
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
                }
                catch (Exception ex)
                {

                    _logger.LogError(ex.ToString());
                    Thread.Sleep(TimeSpan.FromSeconds(60));
                }
                finally
                {

                }

            } while (true);

        }

        private int UpdatePluginFiles(FtpClient ftpClient, FtpServerConfig remoteFtpServerConfig, int updateCount, CancellationToken cancellationToken)
        {
            try
            {
                if (!Directory.Exists(_pluginPath))
                {
                    Directory.CreateDirectory(_pluginPath);
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
                        var updateStatus = DownloadPlugin(ftpClient, pluginInfo, _pluginPath, remotePluginRootDir, tempDirectory);
                        if (updateStatus == UpdatePluginResult.Failed)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(Random.Shared.Next(10, 60)));
                            _logger.LogError($"Download fail:{pluginInfo.name}");
                            continue;
                        }
                        if (updateStatus == UpdatePluginResult.Success)
                        {
                            _logger.LogInformation($"Update plugin{pluginInfo.name} success");
                            updateCount++;
                        }
                        else if (updateStatus == UpdatePluginResult.Skipped)
                        {
                            _logger.LogInformation($"Skip Update plugin{pluginInfo.name}");
                        }

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
                        if (!pluginInfo.launch)
                        {
                            _logger.LogInformation($"Skip launch {pluginInfo.name}");
                            continue;
                        }
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
                        string cmdArgs = $"--parentprocessid {Process.GetCurrentProcess().Id}";
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

        enum UpdatePluginResult
        {
            Success,
            Skipped,
            Failed
        }

        private UpdatePluginResult DownloadPlugin(FtpClient ftpClient,
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
                    var hashFile = Path.Combine(localPluginDir, "sha256.bat");
                    if (File.Exists(hashFile))
                    {
                        var hash = File.ReadAllText(hashFile);
                        if (pluginInfo.hash == hash)
                        {
                            _logger.LogInformation($"Skip update plugin:{pluginInfo.name} because same hash");
                            return UpdatePluginResult.Skipped;
                        }
                        else
                        {
                            _logger.LogInformation($"updating plugin:{pluginInfo.name}");
                        }
                    }
                    CleanupFiles(localPluginDir);
                }
                var downloadUrl = Path.Combine(remotePluginRootDir, pluginInfo.name, pluginInfo.version, RuntimeInformation.OSArchitecture.ToString(), pluginInfo.filename);
                if (!ftpClient.FileExists(downloadUrl))
                {
                    _logger.LogError($"[FTP]could not download plugin {downloadUrl}");
                    return UpdatePluginResult.Failed;
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
                        return UpdatePluginResult.Failed;
                    }
                }
                ZipFile.ExtractToDirectory(tempFileName, localPluginDir, true);
                File.Delete(tempFileName);
                return UpdatePluginResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            return UpdatePluginResult.Failed;
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
            var remoteConfigDirectory = Path.Combine(_remoteServerConfigRoot, remoteFtpServerConfig.version);
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
                    continue;
                }
                var compareResult = ftpClient.CompareFile(localConfigFilePath, remoteConfigFilePath, FtpCompareOption.Size | FtpCompareOption.DateModified);
                _logger.LogInformation($"Compare result:{compareResult} {remoteConfigFilePath}");
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
