using CommandLine;
using MaccorUploadTool.Data;
using MaccorUploadTool.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodeService.Infrastructure;
using NodeService.Infrastructure.Models;
using NodeService.Infrastructure.NodeSessions;
using System.Diagnostics;
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
                    if (options.Mode == null)
                    {
                        options.Mode = "Stat";
                    }
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
                HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

                builder.Services.AddSingleton<Options>(options);
                builder.Services.AddSingleton<HttpClient>(sp =>
                              new HttpClient
                              {
                                  BaseAddress = new Uri("http://172.27.242.223:50060/")
                              }
                              );
                builder.Services.AddSingleton<ApiService>();
                switch (options.Mode)
                {
                    case "Stat":
                        break;
                    case "Kafka":
                        builder.Services.AddHostedService<MaccorDataUploadKafkaService>();
                        break;
                    case "ServerKafka":
                        break;
                    case "Ftp":
                        builder.Services.AddHostedService<MaccorDataUploadFtpService>();
                        break;
                    default:
                        break;
                }
   
                builder.Services.AddHostedService<ProcessService>();
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();

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
