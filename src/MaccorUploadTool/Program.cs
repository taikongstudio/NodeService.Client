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
                                  BaseAddress = new Uri(Debugger.IsAttached ? "http://localhost:5000" : "http://172.27.242.223:50060/")
                              }
                              );
                builder.Services.AddSingleton<ApiService>();
                builder.Services.AddSingleton<MaccorDataReaderWriter>();
                builder.Services.AddHostedService<MaccorDataUploadService>();
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
