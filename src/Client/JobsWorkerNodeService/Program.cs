using CommandLine;
using JobsWorker.Shared;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorker.Shared.Models;
using JobsWorkerNodeService.Models;
using JobsWorkerNodeService.Services;
using NLog;
using NLog.Web;
using Quartz;
using Quartz.Impl;
using System.Diagnostics;

namespace JobsWorkerNodeService
{
    public static class Program
    {
        private static WebApplicationBuilder ConfigureServices(
            this WebApplicationBuilder webApplicationBuilder,
            Action<IServiceCollection> action)
        {
            action(webApplicationBuilder.Services);
            return webApplicationBuilder;
        }

        public static void Main(string[] args)
        {
            Parser
                .Default
                .ParseArguments<Options>(args)
                                     .WithParsed((options) =>
                                     {
                                         if (options.mode==null)
                                         {
                                             options.mode = "nodeservice";
                                         }
                                         RunWithOptions(options, args);
                                     });
        }

        private static void RunWithOptions(Options options, string[] args)
        {
            try
            {
                switch (options.mode)
                {
                    case "nodeservice":
                        RunAsNodeService(options, args);
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

        private static void RunAsNodeService(Options options, string[] args)
        {
            try
            {
                LogManager.AutoShutdown = true;
                var builder = WebApplication.CreateBuilder(args);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<Options>((serviceProvider) =>
                    {
                        if (options.address == null)
                        {
                            options.address = builder.Configuration.GetSection("GrpcConfig")["address"];
                        }
                        return options;
                    });
                    services.AddHostedService<GrpcService>();
                    services.AddHostedService<NodeConfigService>();
                    services.AddHostedService<ProcessService>();
                    services.AddHostedService<UploadFileToFtpServerService>();
                    services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory());
                    services.AddSingleton<IConfigurationStore>(new ConfigurationStore());
                    services.AddSingleton<
                    IInprocRpc<string,
                    string,
                    RequestMessage,
                    ResponseMessage>>(new InprocRpc<string, string, RequestMessage, ResponseMessage>());

                    services.AddSingleton<
                        IInprocMessageQueue<string,
                        string,
                        Message>>(new InprocMessageQueue<string, string, Message>());

                    services.AddSingleton<
                        IInprocMessageQueue<string,
                        string,
                        NodeConfigChangedEvent>>(new InprocMessageQueue<string, string, NodeConfigChangedEvent>());

                    services.AddSingleton<
                        IInprocMessageQueue<string,
                        string,
                        UploadFileToFtpServerRequest>>(new InprocMessageQueue<string, string, UploadFileToFtpServerRequest>());

                    services.AddRazorPages();
                });
                builder
                    .Logging
                    .ClearProviders()
                    .AddConsole()
                    .AddNLogWeb();

                // Add services to the container.
                builder.Services.AddRazorPages();

                var app = builder.Build();

                // Configure the HTTP request pipeline.
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Error");
                }
                app.UseStaticFiles();

                app.UseRouting();

                app.UseAuthorization();

                app.MapRazorPages();

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
