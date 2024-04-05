using Microsoft.Extensions.Logging;
using NodeService.Infrastructure.Interfaces;
using NodeService.Infrastructure.NodeSessions;
using NodeService.WindowsService.Collections;
using Python.Deployment;
using Python.Runtime;
using System.Runtime.CompilerServices;

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
                      }
                      return RunWithOptions(options, args);
                  });
        }

        private static async Task RunWithOptions(Options options, string[] args)
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
                switch (options.mode)
                {
                    case "WindowsService":
                        await RunAsWindowsServiceAsync(options, args);
                        break;
                    default:
                        Console.WriteLine($"Unknown mode:{options.mode}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static async Task RunAsWindowsServiceAsync(Options options, string[] args)
        {
            try
            {
                Environment.CurrentDirectory = AppContext.BaseDirectory;
                await InstallPythonPackageAsync();
                LogManager.AutoShutdown = true;
                HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
                builder.Services.AddWindowsService(options =>
                {
                    options.ServiceName = "NodeService.WindowsService";
                });
                builder.Services.AddSingleton(options);
                builder.Services.AddSingleton<INodeIdentityProvider, NodeIdentityProvider>();
                builder.Services.AddSingleton<JobExecutionContextDictionary>();
                builder.Services.AddHostedService<JobHostService>();
                builder.Services.AddHostedService<NodeClientService>();
                builder.Services.AddHostedService<ProcessService>();
                builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
                builder.Services.AddSingleton<IAsyncQueue<JobExecutionContext>, AsyncQueue<JobExecutionContext>>();
                builder.Services.AddSingleton<IAsyncQueue<JobExecutionReport>, AsyncQueue<JobExecutionReport>>();
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.AddNLog("NLog.config");

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

        private static async Task InstallPythonPackageAsync()
        {
            try
            {
                Installer.Source = new Installer.EmbeddedResourceInstallationSource()
                {
                    Assembly = typeof(Job).Assembly,
                    Force = true,
                    ResourceName = "python-3.8.5-embed-amd64.zip"
                };
                string pythonDir = "python-3.8.5-embed-amd64";
                if (Directory.Exists(pythonDir))
                {
                    Directory.Delete(pythonDir, true);
                }
                // install in local directory. if you don't set it will install in local app data of your user account
                Installer.InstallPath = Path.GetFullPath(AppContext.BaseDirectory);

                // see what the installer is doing
                Installer.LogMessage += Console.WriteLine;


                // install from the given source
                await Installer.SetupPython(true);

                // ok, now use pythonnet from that installation
                PythonEngine.Initialize();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

    }
}
