using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NodeService.DeviceHost.Devices;
using NodeService.DeviceHost.Models;
using NodeService.DeviceHost.Services;
using NodeService.ServiceHost.Models;
using System.Diagnostics;
using System.Text;

namespace NodeService.DeviceHost
{
    internal class Program
    {
        static async Task Main(string[] args)
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
                EnsureOptions(options);
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Environment.CurrentDirectory = AppContext.BaseDirectory;
                LogManager.AutoShutdown = true;
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", options.env);
                var builder = Host.CreateApplicationBuilder(args);
                builder.Configuration.AddJsonFile(
                    $"appsettings.{(builder.Environment.IsDevelopment()
                    ? "Development." : string.Empty)}json",
                    false, true);

                builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("ServerOptions"));
                builder.Services.AddSingleton<ServiceOptions>(options);
                builder.Services.AddSingleton<DeviceFactory>();
                builder.Services.AddHostedService<DeviceService>();

                builder.Services.AddHttpClient();
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.AddNLog($"NLog.config");

                using var app = builder.Build();

                await app.RunAsync();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                LogManager.Flush();
                LogManager.Shutdown();
            }

            static void EnsureOptions(ServiceOptions options)
            {
                if (string.IsNullOrEmpty(options.mode))
                {
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
                var env = Environment.GetEnvironmentVariable("NodeServiceEnvironments", EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrEmpty(env))
                {
                    options.env = env;
                }
            }
        }
    }
}
