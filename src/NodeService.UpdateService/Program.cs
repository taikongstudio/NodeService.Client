using CommandLine;
using NLog;
using NLog.Web;
using NodeService.ServiceProcess;
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
                    services.AddSingleton(options);
                    services.Configure<ServiceProcessConfiguration>(hostContext.Configuration.GetSection("AppConfig"));
                    services.AddHostedService<DetectServiceStatusService>();
                    services.AddHostedService<ProcessExitService>();
                    services.AddWindowsService(options =>
                    {
                        options.ServiceName = "NodeService.UpdateService";
                    });
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
