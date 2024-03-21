using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;

using NodeService.UpdateService.Models;
using NodeService.UpdateService.Services;

namespace NodeService.UpdateService
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Parser
                .Default
                .ParseArguments<Options>(args)
                .WithParsed((options) =>
                {
                    RunWithOptions(options, args);
                });



        }

        private static void RunWithOptions(Options options, string[] args)
        {

            try
            {
                LogManager.AutoShutdown = true;
                IHostBuilder builder = Host.CreateDefaultBuilder(args);

                builder.ConfigureServices((hostContext, services) =>
                {
                    services.Configure<UpdateConfig>(hostContext.Configuration.GetSection("UpdateConfig"));
                    services.AddSingleton(options);
                    services.AddHostedService<UpdatePackageService>();
                    services.AddWindowsService();

                }).ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole();
                })
                .UseNLog();

                using var app = builder.Build();

                app.Run();
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
