﻿using CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NLog;
using NodeService.Infrastructure.Messages;
using NodeService.Infrastructure.NodeSessions;
using NodeService.ServiceHost.Models;
using NodeService.ServiceHost.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.Design.AxImporter;
using Debugger = System.Diagnostics.Debugger;

namespace NodeService.ServiceHost
{
    public static class Program
    {
        public static async Task Main(string[] args)
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
                if (!CheckEnvironment())
                {
                    return;
                }
                EnsureOptions(options);
                Environment.CurrentDirectory = AppContext.BaseDirectory;
                LogManager.AutoShutdown = true;
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", options.env);
                var builder = Host.CreateApplicationBuilder(args);
                builder.Configuration.AddJsonFile(
                    $"appsettings.{(builder.Environment.IsDevelopment() 
                    ? "Development." : string.Empty)}json",
                    false, true);

                builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("ServerOptions"));
                builder.Services.AddSingleton<INodeIdentityProvider, NodeIdentityProvider>();
                builder.Services.AddSingleton<TaskExecutionContextDictionary>();
                builder.Services.AddSingleton<ServiceOptions>(options);
                builder.Services.AddSingleton<IAsyncQueue<TaskExecutionContext>, AsyncQueue<TaskExecutionContext>>();
                builder.Services.AddSingleton<IAsyncQueue<JobExecutionReport>, AsyncQueue<JobExecutionReport>>();

                builder.Services.AddHostedService<TaskHostService>();
                builder.Services.AddHostedService<NodeClientService>();
                builder.Services.AddHostedService<PythonRuntimeService>();
                builder.Services.AddHostedService<ProcessServerService>();

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
                    options.mode = "WindowsService";
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
            }
        }

        static bool CheckEnvironment()
        {
            if (!Environment.IsPrivilegedProcess)
            {
                Console.WriteLine("Need Privileged Process");
                return false;
            }
            return true;
        }


    }
}
