using CommandLine;
using MaccorUploadTool.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodeService.Infrastructure.Models;
using System.Text.Json;

namespace MaccorUploadTool
{
    public class Program
    {
        static void Main(string[] args)
        {
            Parser
                .Default
                .ParseArguments<Options>(args)
                .WithParsed((options) =>
                {
                    RunWithOptions(options, args);
                })
                .WithNotParsed(PrintErrors);
        }

        private static void PrintErrors(IEnumerable<Error> errors)
        {

        }

        private static void RunWithOptions(Options options, string[] args)
        {
            try
            {
                IHostBuilder builder = Host.CreateDefaultBuilder(args);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<Options>(options);
                    services.AddHostedService<MaccorDataUploadBackgroundService>();
                    services.AddSingleton<UploadMaccorDataJobOptions>(sp =>
                    {
                        var jsonText = File.ReadAllText(options.ConfigFilePath);
                        return JsonSerializer.Deserialize<UploadMaccorDataJobOptions>(jsonText);
                    });


                }).ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole();
                });

                using var app = builder.Build();

                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
