using CommandLine;
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
                    if (options.ParentProcesssId == null)
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
                LogManager.Flush();
                LogManager.Shutdown();
            }

        }



    }
}
