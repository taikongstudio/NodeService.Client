using CommandLine;
using JobsWorkerNode.Workers;
using MaccorUploadTool.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using System.Diagnostics;

namespace MaccorUploadTool
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
                    if (options.ParentProcesssId != null)
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
                        services.AddHostedService<DataFileCollectorWorker>();
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
                if (int.TryParse(options.ParentProcesssId, out var processId))
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

    }
}
