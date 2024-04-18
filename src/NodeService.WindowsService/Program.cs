using NodeService.Infrastructure.NodeSessions;
using NodeService.ServiceProcess;
using NodeService.WindowsService.Models;
using Python.Deployment;
using Python.Runtime;
using System.Management;

namespace NodeService.WindowsService
{
    public static class Program
    {

        public static async Task Main(string[] args)
        {
       
            await Parser
                  .Default
                  .ParseArguments<Options>(args)
                  .WithParsedAsync((options) =>
                  {
                      if (options.mode == null)
                      {
                          options.mode = "WindowsService";
                          if (options.env == null)
                          {
                              options.env = Environments.Production;
                          }
                      }
                      if (options.env == null|| Debugger.IsAttached)
                      {
                          options.env = Environments.Development;
                      }
                      return RunWithOptionsAsync(options, args);
                  });
        }

        private static async Task RunWithOptionsAsync(Options options, string[] args)
        {
            try
            {
                if (!Environment.IsPrivilegedProcess)
                {
                    Console.WriteLine("Need Privileged Process,exit");
                    Console.Out.Flush();
                    Environment.Exit(-1);
                    return;
                }
                Console.WriteLine(JsonSerializer.Serialize(options));
                Console.WriteLine($"ClientVersion:{Constants.Version}");
                if (options.mode == "Uninstall")
                {
                    const string DaemonServiceName = "NodeService.DaemonService";
                    const string UpdateServiceName = "NodeService.UpdateService";
                    const string WindowsServiceName = "NodeService.WindowsService";
                    const string WorkerServiceName = "NodeService.WorkerService";
                    const string JobsWorkerDaemonServiceName = "JobsWorkerDaemonService";
                    await foreach (var progress in ServiceProcessInstallerHelper.UninstallAllService(
                    [
                        WorkerServiceName,
                        UpdateServiceName,
                        WindowsServiceName,
                        JobsWorkerDaemonServiceName
                    ]))
                    {
                        Console.WriteLine(progress.Message);
                    }
                }
                else
                {
                    await RunAsync(options, args);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {

            }
        }

        private static async Task RunAsync(Options options, string[] args)
        {
            try
            {
                Environment.CurrentDirectory = AppContext.BaseDirectory;
                LogManager.AutoShutdown = true;
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", options.env);
                HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
                builder.Configuration.AddJsonFile(
                    $"{options.mode}.appsettings.{(builder.Environment.IsDevelopment() ? "Development." : string.Empty)}json",
                    false,
                    true);

                builder.Services.AddWindowsService(windowsServiceOptions =>
                {
                    windowsServiceOptions.ServiceName = $"NodeService.{options.mode}";
                });
                builder.Services.AddSingleton(options);
                builder.Services.Configure<ServiceProcessConfiguration>(builder.Configuration.GetSection("ServiceProcessConfiguration"));
                builder.Services.AddHostedService<DetectServiceStatusService>();
                builder.Services.AddHostedService<ProcessExitService>();
                if (options.mode == "WindowsService")
                {
                    builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("ServerConfiguration"));
                    builder.Services.AddSingleton<INodeIdentityProvider, NodeIdentityProvider>();
                    builder.Services.AddSingleton<JobExecutionContextDictionary>();
                    builder.Services.AddHostedService<JobHostService>();
                    builder.Services.AddHostedService<NodeClientService>();
                    builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
                    builder.Services.AddSingleton<IAsyncQueue<JobExecutionContext>, AsyncQueue<JobExecutionContext>>();
                    builder.Services.AddSingleton<IAsyncQueue<JobExecutionReport>, AsyncQueue<JobExecutionReport>>();
                }
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.AddNLog($"{options.mode}.NLog.config");

                using var app = builder.Build();

                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                LogManager.Flush();
                LogManager.Shutdown();
            }

        }

    }
}
