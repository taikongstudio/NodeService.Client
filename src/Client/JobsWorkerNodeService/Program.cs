using CommandLine;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorkerNodeService.Models;
using JobsWorkerNodeService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using Quartz;
using Quartz.Impl;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace JobsWorkerNodeService
{
    public static class Program
    {

        public static void Main(string[] args)
        {
            Parser
                .Default
                .ParseArguments<Options>(args)
                                     .WithParsed((options) =>
                                     {
                                         if (options.mode == null)
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
                Console.WriteLine(JsonSerializer.Serialize(options));
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
                IHostBuilder builder = Host.CreateDefaultBuilder(args);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<Options>(options);
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

                }).ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole();
                })
                .UseNLog();

                var app = builder.Build();

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
