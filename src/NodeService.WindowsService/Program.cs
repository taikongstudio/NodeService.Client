using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Extensions.Logging;
using NodeService.Infrastructure;
using NodeService.Infrastructure.Messages;
using NodeService.Infrastructure.Models;
using NodeService.Infrastructure.NodeSessions;
using NodeService.ServiceProcess;
using NodeService.WindowsService.Models;
using NodeService.WindowsService.Services;

namespace NodeService.WindowsService
{
    public static class Program
    {

        public static async Task Main(string[] args)
        {
            await Parser
                  .Default
                  .ParseArguments<ServiceOptions>(args)
                  .WithParsedAsync((options) =>
                  {
                      return RunWithOptionsAsync(options, args);
                  });
        }

        private static async Task RunWithOptionsAsync(ServiceOptions options, string[] args)
        {
            try
            {
                if (!CheckEnvironment())
                {
                    return;
                }

                EnsureOptions(options);
                Console.WriteLine(JsonSerializer.Serialize(options));
                Console.WriteLine($"ClientVersion:{Constants.Version}");
                if (options.mode.Equals("Uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    const string DaemonServiceName = "NodeService.DaemonService";
                    const string UpdateServiceName = "NodeService.UpdateService";
                    const string WindowsServiceName = "NodeService.WindowsService";
                    const string WorkerServiceName = "NodeService.WorkerService";
                    const string JobsWorkerDaemonServiceName = "JobsWorkerDaemonService";
                    await foreach (var progress in ServiceProcessInstallerHelper.UninstallAllService(
                    [
                        DaemonServiceName,
                        WorkerServiceName,
                        UpdateServiceName,
                        WindowsServiceName,
                        JobsWorkerDaemonServiceName
                    ]))
                    {
                        Console.WriteLine(progress.Message);
                    }
                }
                else if (options.mode.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
                {

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
                Console.Out.Flush();
            }

            static void EnsureOptions(ServiceOptions options)
            {
                if (string.IsNullOrEmpty(options.mode))
                {
                    options.mode = "WindowsService";
                    if (options.env == null)
                    {
                        options.env = Environments.Production;
                    }
                }
                if (options.env == null)
                {
                    if (Debugger.IsAttached)
                    {
                        options.env = Environments.Development;
                    }
                    else
                    {
                        options.env = Environments.Production;
                    }
                }
            }
        }

        private static bool CheckEnvironment()
        {
            if (!Environment.IsPrivilegedProcess)
            {
                Console.WriteLine("Need Privileged Process");
                return false;
            }
            return true;
        }

        private static async Task RunAsync(ServiceOptions options, string[] args)
        {

            try
            {
                Environment.CurrentDirectory = AppContext.BaseDirectory;
                LogManager.AutoShutdown = true;
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", options.env);
                var builder = WebApplication.CreateBuilder(args);
                builder.Configuration.AddJsonFile(
                    $"{options.mode}.appsettings.{(builder.Environment.IsDevelopment() ? "Development." : string.Empty)}json",
                    false, true);
                builder.Services.Configure<ServiceProcessConfiguration>(builder.Configuration.GetSection("ServiceProcessConfiguration"));
                builder.Services.Configure<ServiceOptions>(builder.Configuration.GetSection("ServerOptions"));
                builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("ServerOptions"));
                string serviceName = $"NodeService.{options.mode}";
                builder.Services.AddWindowsService(windowsServiceOptions =>
                {
                    windowsServiceOptions.ServiceName = serviceName;
                });

                builder.Services.AddSingleton(options);
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.ListenNamedPipe(serviceName, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
                //builder.Services.AddHostedService<DetectServiceStatusService>();
                builder.Services.AddHostedService<ProcessExitService>();


                if (options.mode == "WindowsService")
                {
                    builder.Services.AddHostedService<ServiceHostService>();
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
