using JobsWorkerWebService.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;
using System.IO;
using System.Runtime;

namespace JobsWorkerNodeService.Jobs
{
    public class GCJob : JobBase
    {
        public override Task Execute(IJobExecutionContext context)
        {
            GCImpl();
            return Task.CompletedTask;
        }

        private void GCImpl()
        {
            try
            {
                if (Arguments != null)
                {
                    if (!bool.Parse(Arguments["GCEnabled"]))
                    {
                        return;
                    }
                }
                DeleteUnusedJobWorkerFiles();
                DeleteDaemonServiceUnsedPlugins();
                DeleteJobsWorkerUnsedPlugins();
                DeleteJobWorkerLogFiles();
                DeleteDaemonServiceLogFiles();
            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex.ToString());
            }

        }

        private void DeleteJobsWorkerUnsedPlugins()
        {
            try
            {
                Logger.LogInformation("GC Begin");
                var configPath = Path.Combine(AppContext.BaseDirectory, "config", "server.bat");
                if (!DeviceConfiguration.TryLoadServerConfig(configPath, Logger, out DeviceConfiguration ftpServerConfig))
                {
                    Logger.LogInformation("Load server config fail");
                    return;
                }

                var pluginRootDir = Path.Combine(AppContext.BaseDirectory, "plugins");
                if (!Directory.Exists(pluginRootDir))
                {
                    return;
                }

                foreach (var pluginDir in Directory.GetDirectories(pluginRootDir))
                {
                    try
                    {
                        var pluginDirName = Path.GetFileName(pluginDir);
                        if (!ftpServerConfig.plugins.Any(x => x.plugin_name == pluginDirName))
                        {
                            Directory.Delete(pluginDir, true);
                            Logger.LogInformation($"[GC]Deleted:{pluginDir}");
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            finally
            {
                Logger.LogInformation("GC End");
            }
        }

        private void DeleteDaemonServiceUnsedPlugins()
        {
            try
            {
                Logger.LogInformation("GC Begin");
                var currentJobWorkerDirInfo = new DirectoryInfo(AppContext.BaseDirectory);
                var pluginRootDir = currentJobWorkerDirInfo.Parent.Parent.FullName;
                if (!Directory.Exists(pluginRootDir))
                {
                    return;
                }

                foreach (var pluginDir in Directory.GetDirectories(pluginRootDir))
                {
                    try
                    {
                        var pluginDirName = Path.GetFileName(pluginDir);
                        if (pluginDirName == "JobsWorkerNode")
                        {
                            Logger.LogInformation("Skip delete JobsWorkerNode dir");
                            continue;
                        }
                        Directory.Delete(pluginDir, true);
                        Logger.LogInformation($"[GC]Deleted:{pluginDir}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            finally
            {
                Logger.LogInformation("GC End");
            }
        }

        private void DeleteDaemonServiceLogFiles()
        {
            try
            {
                Logger.LogInformation("GC Begin");
                var currentJobWorkerDirInfo = new DirectoryInfo(AppContext.BaseDirectory);
                var daemonServiceDir = currentJobWorkerDirInfo.Parent.Parent.Parent.FullName;
                var logsPath = Path.Combine(daemonServiceDir, "logs");
                if (!Directory.Exists(logsPath))
                {
                    return;
                }

                foreach (var logFileName in Directory.GetFiles(logsPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFileName);
                        if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromDays(7))
                        {
                            fileInfo.Delete();
                            Logger.LogInformation($"[GC]Deleted:{fileInfo.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }

                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            finally
            {
                Logger.LogInformation("GC End");
            }
        }

        private void DeleteJobWorkerLogFiles()
        {
            try
            {
                var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
                if (!Directory.Exists(logsPath))
                {
                    return;
                }

                foreach (var logFileName in Directory.GetFiles(logsPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFileName);
                        if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromDays(7))
                        {
                            fileInfo.Delete();
                            Logger.LogInformation($"[GC]Deleted:{fileInfo.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }

                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        private void DeleteUnusedJobWorkerFiles()
        {
            try
            {
                var currentJobWorkerInfo = new DirectoryInfo(AppContext.BaseDirectory);
                var jobWorkerRootDirInfo = new DirectoryInfo(currentJobWorkerInfo.Parent.FullName);
                foreach (var item in Directory.GetDirectories(jobWorkerRootDirInfo.FullName))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(item);
                        if (dirInfo.Name == currentJobWorkerInfo.Name) { continue; }
                        Directory.Delete(dirInfo.FullName, true);
                        Logger.LogInformation($"[GC]Deleted:{dirInfo.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
    }
}
