using CommandLine;
using JobRunner.Services;
using JobsWorker.Shared.DataModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using System.Text.Json;

namespace JobRunner
{
    public class Program
    {
        public static void Main(string[] args)
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
                IHostBuilder builder = Host.CreateDefaultBuilder(args);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<Options>(options);
                    services.AddSingleton<JobScheduleConfigModel>((serviceProvider) =>
                    {
                        var jsonText = File.ReadAllText(options.jobConfigPath.Trim('\"'));
                        JobScheduleConfigModel jobScheduleConfig = JsonSerializer.Deserialize<JobScheduleConfigModel>(jsonText);
                        return jobScheduleConfig;
                    });
                    services.AddSingleton<NodeConfigTemplateModel>((serviceProvider) =>
                    {
                        var jsonText = File.ReadAllText(options.nodeConfigPath.Trim('\"'));
                        NodeConfigTemplateModel nodeConfig = JsonSerializer.Deserialize<NodeConfigTemplateModel>(jsonText);
                        return nodeConfig;
                    });
                    services.AddHostedService<JobScheduleService>();
                    services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory());

                }).ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole();
                });


                var app = builder.Build();

                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
