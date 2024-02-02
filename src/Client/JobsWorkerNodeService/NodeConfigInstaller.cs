using JobsWorker.Shared;
using JobsWorker.Shared.Models;
using JobsWorkerNodeService.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace JobsWorkerNodeService
{
    public class NodeConfigInstaller
    {
        public string PluginsDirectory { get; private set; }

        public ILogger Logger { get; private set; }

        public IScheduler Scheduler { get; private set; }

        public  ConcurrentDictionary<string, JobScheduleTask> TaskDictionary { get; private set; }

        public NodeConfigInstaller(
            ILogger logger,
            IScheduler scheduler,
            ConcurrentDictionary<string, JobScheduleTask> taskDict)
        {
            this.Logger = logger;
            this.Scheduler = scheduler;
            this.TaskDictionary = taskDict;
        }

        private bool ValidatePluginsConfig(NodeConfig nodeConfig)
        {
            if (nodeConfig.plugins == null || nodeConfig.plugins.Length == 0)
            {
                return true;
            }
            if (nodeConfig.plugins != null)
            {
                foreach (var pluginInfo in nodeConfig.plugins)
                {
                    var localPluginDir = Path.Combine(this.PluginsDirectory, pluginInfo.pluginName, pluginInfo.version);
                    if (!Directory.Exists(localPluginDir))
                    {
                        this.Logger.LogError($"missing directory:{localPluginDir}");
                        return false;
                    }
                    var entryPoint = Path.Combine(localPluginDir, pluginInfo.entryPoint);
                    if (!File.Exists(entryPoint))
                    {
                        this.Logger.LogError($"missing entryPoint:{entryPoint}");
                        return false;
                    }
                }
            }

            return true;
        }

        public async Task<bool> InstallAsync(NodeConfig nodeConfig)
        {
            try
            {
                await this.ApplyNodeConfig(nodeConfig);
                return true;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

            return false;
        }


        private bool ShouldTestRemoteCondig(NodeConfig currentConfig, NodeConfig newConfig)
        {
            return true;
        }

        private async Task KillPluginProcessByProcessNameAsync(string processName, CancellationToken cancellationToken = default)
        {
            try
            {
                do
                {

                    try
                    {
                        this.Logger.LogInformation($"[KillPluginProcess] try kill process:{processName}");

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        var processes = Process.GetProcessesByName(processName);

                        if (processes.Length == 0)
                        {
                            this.Logger.LogInformation($"[KillPluginProcess] no plugin process:{processName}");
                            break;
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        foreach (var process in processes)
                        {
                            try
                            {
                                process.Kill(true);
                                this.Logger.LogInformation($"[KillPluginProcess] kill ProcessName:{process.ProcessName} processId:{process.Id}");
                            }
                            catch (Exception ex)
                            {
                                this.Logger.LogError(ex.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                        this.Logger.LogError(ex.ToString());
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                    finally
                    {

                    }

                } while (!cancellationToken.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }


        }

        private async Task InstallAllPluginsAsync(JobsWorkerApiService jobsWorkerApiService, PluginInfo[] plugins, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(this.PluginsDirectory))
                {
                    Directory.CreateDirectory(this.PluginsDirectory);
                }
                string tempDownloadDirectory = EnsureTempDirectory();

                foreach (var pluginInfo in plugins)
                {
                    await this.InstallPluginAsync(jobsWorkerApiService, tempDownloadDirectory, pluginInfo, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
        }

        private async Task InstallPluginAsync(JobsWorkerApiService jobsWorkerApiService, string tempDownloadDirectory, PluginInfo pluginInfo, CancellationToken cancellationToken)
        {
            try
            {
                var pluginInstallDirectory = Path.Combine(this.PluginsDirectory, pluginInfo.pluginName, pluginInfo.version);
                var tempPluginInstallDirectory = Path.Combine(this.PluginsDirectory, "temp", Guid.NewGuid().ToString());
                while (!cancellationToken.IsCancellationRequested)
                {
                    var updateStatus = await this.DownloadPluginAsync(jobsWorkerApiService, pluginInfo, tempPluginInstallDirectory, tempDownloadDirectory);
                    if (updateStatus == DownloadPluginResult.Failed)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)));
                        this.Logger.LogError($"Download fail:{pluginInfo.pluginName}");
                        continue;
                    }
                    if (updateStatus == DownloadPluginResult.Success)
                    {
                        while (true)
                        {
                            await this.KillPluginProcessByProcessNameAsync(pluginInfo.pluginName, cancellationToken);
                            if (CleanupDirectory(pluginInstallDirectory))
                            {
                                Directory.Move(tempPluginInstallDirectory, pluginInstallDirectory);
                                this.Logger.LogInformation($"Update plugin{pluginInfo.pluginName} success");
                                break;
                            }
                            this.Logger.LogInformation($"Cleanup directory {pluginInstallDirectory} fail");
                            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 60)));
                            continue;
                        }
                        break;
                    }
                    else if (updateStatus == DownloadPluginResult.Skipped)
                    {
                        this.Logger.LogInformation($"Skip download plugin{pluginInfo.pluginName}");
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

        private bool TestNodeServiceConfiguration(NodeConfig nodeConfig)
        {
            try
            {
                return true;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
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
                CleanupDirectory(tempPluginDirectory);
            }
            return tempPluginDirectory;
        }


        enum DownloadPluginResult
        {
            Success,
            Skipped,
            Failed
        }


        private void StartPluginProcesses(NodeConfig nodeConfig, bool skipIfExits)
        {
            try
            {
                if (nodeConfig.plugins == null)
                {
                    return;
                }
                foreach (var pluginInfo in nodeConfig.plugins)
                {
                    try
                    {
                        if (!pluginInfo.launch)
                        {
                            this.Logger.LogInformation($"Skip launch {pluginInfo.pluginName}");
                            continue;
                        }
                        var pluginWorkerDir = Path.Combine(this.PluginsDirectory, pluginInfo.pluginName, pluginInfo.version);
                        var entryPoint = Path.Combine(this.PluginsDirectory, pluginInfo.pluginName, pluginInfo.version, pluginInfo.entryPoint);
                        if (!File.Exists(entryPoint))
                        {
                            this.Logger.LogError($"[Plugin]Could not found plugin {pluginInfo.pluginName} exe:{entryPoint}");
                            continue;
                        }

                        if (skipIfExits)
                        {
                            var processes = Process.GetProcessesByName(pluginInfo.pluginName);

                            if (processes.Any())
                            {
                                this.Logger.LogError($"[Plugin]skip create {pluginInfo.pluginName} exe:{entryPoint}");
                                continue;
                            }
                        }
                        string cmdArgs = $"--parentprocessid {Process.GetCurrentProcess().Id}";
                        Process childProcess = Process.Start(entryPoint, cmdArgs);
                        this.Logger.LogInformation($"Start process:{entryPoint} args:{cmdArgs}");
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex.ToString());
                    }

                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

        private async Task<DownloadPluginResult> DownloadPluginAsync(JobsWorkerApiService jobsWorkerApiService,
            PluginInfo pluginInfo,
            string tempPluginExtractDirectory,
            string tempDownloadDirectory)
        {
            try
            {


                if (!Directory.Exists(tempPluginExtractDirectory))
                {
                    Directory.CreateDirectory(tempPluginExtractDirectory);
                }
                else
                {
                    var hashFile = Path.Combine(tempPluginExtractDirectory, "sha256.bat");
                    if (File.Exists(hashFile))
                    {
                        var hash = File.ReadAllText(hashFile);
                        if (pluginInfo.hash == hash)
                        {
                            this.Logger.LogInformation($"Skip update plugin:{pluginInfo.pluginName} because same hash");
                            return DownloadPluginResult.Skipped;
                        }
                        else
                        {
                            this.Logger.LogInformation($"updating plugin:{pluginInfo.pluginName}");
                        }
                    }
                    this.CleanupDirectory(tempPluginExtractDirectory);
                }

                var tempFileName = Path.Combine(tempDownloadDirectory, Guid.NewGuid().ToString());
                if (File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                }
                using (var fileStream = File.OpenWrite(tempFileName))
                {
                    try
                    {
                        fileStream.Position = 0;
                        var stream = await jobsWorkerApiService.DownloadPluginFileAsync(pluginInfo);
                        await stream.CopyToAsync(fileStream);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError($"[FTP]could not download plugin {pluginInfo.downloadUrl}");
                        return DownloadPluginResult.Failed;
                    }
                }
                ZipFile.ExtractToDirectory(tempFileName, tempPluginExtractDirectory, true);
                File.Delete(tempFileName);
                return DownloadPluginResult.Success;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
            return DownloadPluginResult.Failed;
        }

        private bool CleanupDirectory(string directory)
        {
            try
            {
                Directory.Delete(directory, true);
                return true;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
            return false;
        }
        private async Task RescheduleJobAsync(JobScheduleConfig[] jobScheduleConfigs, IEnumerable<NodeInfo> deviceList)
        {
            foreach (var config in jobScheduleConfigs)
            {
                try
                {
                    if (!config.isEnabled)
                    {
                        this.Logger.LogInformation("Skip schedule reason:disabled");
                        continue;
                    }
                    if (!FilterHostName(config))
                    {
                        this.Logger.LogInformation("Skip schedule reason:host filter");
                        continue;
                    }
                    string hostName = Dns.GetHostName();
                    if (!string.IsNullOrEmpty(config.factoryName))
                    {
                        var machine = deviceList.Where(x => x.node_name != null)
                            .FirstOrDefault(x => x.node_name.Equals(hostName, StringComparison.OrdinalIgnoreCase));
                        if (machine == null || machine.factory_name != config.factoryName)
                        {
                            this.Logger.LogInformation($"Skip schedule reason:hostname:{hostName} factory_name:{config.factoryName},machine:{machine?.node_name},factory_name:{machine?.factory_name}");
                            continue;
                        }
                    }
                    var task = new JobScheduleTask(this.Logger, this.Scheduler);
                    await task.ScheduleAsync(config);
                    this.TaskDictionary.TryAdd(task.Config.jobId, task);
                    this.Logger.LogInformation($"Schedule Task:{JsonSerializer.Serialize(config)}");
                }
                catch (Exception ex)
                {
                    this.Logger.LogError($"{config?.jobName}:{ex}");
                }

            }


        }

        private async Task ApplyNodeConfig(NodeConfig nodeConfig)
        {
            using var jobsWorkerApiClient = new JobsWorkerApiService(nodeConfig.addresses.FirstOrDefault(), nodeConfig.restApis);
            var deviceList = await jobsWorkerApiClient.QueryDeviceListAsync();
            await this.CancelTasksAsync();
            //await this.InstallAllPluginsAsync(jobsWorkerApiClient, nodeConfig.plugins);
            //this.StartPluginProcesses(nodeConfig, true);
            await this.RescheduleJobAsync(nodeConfig.jobScheduleConfigs, deviceList.ToList());
        }

        private bool FilterHostName(JobScheduleConfig config)
        {
            try
            {
                if (config.dnsNameFilters != null && config.dnsNameFilters.Any())
                {
                    var dns = Dns.GetHostName();
                    if (config.dnsNameFilterType == "include" && !config.dnsNameFilters.Any(x => x.Equals(dns, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                    if (config.dnsNameFilterType == "exclude" && config.dnsNameFilters.Any(x => x.Equals(dns, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
            return false;
        }

        private async Task CancelTasksAsync()
        {
            foreach (var jobScheduleTask in this.TaskDictionary.Values)
            {
                await jobScheduleTask.CancelAsync();
            }
            this.TaskDictionary.Clear();
        }
    }
}
