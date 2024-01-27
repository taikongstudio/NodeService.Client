using CommandLine;
using JobsWorkerNode.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using Quartz;
using Quartz.Impl;
using System.Diagnostics;

namespace JobsWorkerNode
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                 .WithParsed((options) =>
                 {
                     RunWithOptions(options, args);
                 });
        }

        private static void RunWithOptions(Options options, string[] args)
        {
            Logger logger = null;
            try
            {
                Environment.CurrentDirectory = AppContext.BaseDirectory;
                const string logConfigFileName = "Nlog.config";
                if (!File.Exists(logConfigFileName))
                {
                    Environment.Exit(0);
                    return;
                }
                logger = NLogBuilder.ConfigureNLog(logConfigFileName).GetCurrentClassLogger();
                logger.Log(NLog.LogLevel.Info, $"args:{Environment.CommandLine}");
                LogManager.AutoShutdown = true;
                if (!Debugger.IsAttached)
                {
                    if (options.parentprocessid != null)
                    {
                        DetectParentProcess(options, logger);
                    }
                    else
                    {
                        logger.Log(NLog.LogLevel.Error, $"no parent processid");
                        return;
                    }
                }
                logger.Log(NLog.LogLevel.Info, $"VersionGuid:{Constants.Version}");
                IHost host = Host.CreateDefaultBuilder(args)

                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(options);
                        services.AddHostedService<UpdateConfigWorker>();
                        services.AddHostedService<JobWorker>();
                        services.AddHostedService<ServiceWorker>();
                        services.AddHostedService<UploadLogsJobWorker>();
                        services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory());
                    })
                    .ConfigureLogging(loggingBuilder =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddConsole();
                    })
                    .UseNLog()
                    .Build();

                host.Run();
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Fatal(ex);
                }
            }
            finally
            {
                LogManager.Shutdown();
            }

        }

        private static void DetectParentProcess(Options options, Logger logger)
        {
            var task = new Task(async () =>
            {
                if (int.TryParse(options.parentprocessid, out var processId))
                {
                    while (true)
                    {
                        try
                        {
                            using var process = Process.GetProcessById(processId);
                            if (process == null)
                            {
                                return;
                            }
                            try
                            {
                                if (process.HasExited)
                                {
                                    goto LExit;
                                }
                            }
                            catch
                            {

                            }
                            //KillNonServiceDaemonServiceProcess(logger, processId);
                        }
                        catch (ArgumentException ex)
                        {
                            logger.Log(NLog.LogLevel.Error, ex.ToString());
                            goto LExit;

                        }
                        catch (Exception ex)
                        {
                            logger.Log(NLog.LogLevel.Error, ex.ToString());
                        }

                        await Task.Delay(10000);
                    }

                }
            LExit:
                logger.Log(NLog.LogLevel.Error, $"exit process");
                LogManager.Flush();
                Environment.Exit(0);
            }, default, TaskCreationOptions.LongRunning);
            task.Start();
        }

        private static void KillNonServiceDaemonServiceProcess(Logger logger, int parentProcessId)
        {
            var processes = Process.GetProcessesByName("JobsWorkerDaemonService");
            if (processes.Length == 1 && processes[0].Id != parentProcessId)
            {
                logger.Log(NLog.LogLevel.Fatal, $"Exit parentProcessId:{parentProcessId} JobsWorkerDaemonService processid:{processes[0].Id}");
                LogManager.Flush();
                Environment.Exit(0);
                return;
            }
            foreach (var process in processes)
            {
                try
                {
                    if (process.Id != parentProcessId)
                    {
                        logger.Log(NLog.LogLevel.Info, $"kill {process.Id}");
                        process.Kill();
                        logger.Log(NLog.LogLevel.Info, $"killed {process.Id}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Log(NLog.LogLevel.Error, ex.ToString());
                }
                finally
                {

                }
            }
        }

        private static void DeleteStartup(string directory, string shortcutName, Logger logger)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    //添加引用 Com 中搜索 Windows Script Host Object Model
                    string shortcutPath = Path.Combine(directory, string.Format("{0}.lnk", shortcutName));

                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                        return;
                    }

                }
                catch (Exception ex)
                {
                    logger.Error(ex.ToString());
                }
            });

        }

    }
}
