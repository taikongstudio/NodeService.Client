﻿using Grpc.Net.Client.Configuration;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NodeService.Infrastructure.Concurrent;
using NodeService.ServiceHost.Models;
using System.Net;
using System.Net.Http;
using System.Text;

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
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Environment.CurrentDirectory = AppContext.BaseDirectory;
                LogManager.AutoShutdown = true;
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", options.env);
                var builder = Host.CreateApplicationBuilder(args);
                builder.Configuration.AddJsonFile(
                    $"appsettings.{(builder.Environment.IsDevelopment()
                    ? "Development." : string.Empty)}json",
                    false, true);

                builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(nameof(ServerOptions)));
                builder.Services.AddSingleton<INodeIdentityProvider, NodeIdentityProvider>();
                builder.Services.AddSingleton<TaskExecutionContextDictionary>();
                builder.Services.AddSingleton<ServiceOptions>(options);
                builder.Services.AddSingleton<IAsyncQueue<TaskExecutionContext>, AsyncQueue<TaskExecutionContext>>();
                builder.Services.AddSingleton<IAsyncQueue<TaskExecutionReport>>(new AsyncQueue<TaskExecutionReport>(1024 * 500));
                builder.Services.AddKeyedSingleton<IAsyncQueue<FileSystemWatchEventReport>, AsyncQueue<FileSystemWatchEventReport>>(nameof(NodeClientService));  builder.Services.AddHostedService<TaskHostService>();
                builder.Services.AddHttpClient();
                builder.Services.AddHostedService<NodeClientService>();
                builder.Services.AddHostedService<PythonRuntimeService>();
                builder.Services.AddHostedService<ProcessServerService>();
                builder.Services.AddHostedService<ParentProcessMonitorService>();

                builder.Services.AddGrpcClient<NodeServiceClient>((sp, options) =>
                {
                    var serverOptions = sp.GetService<IConfiguration>().GetSection(nameof(ServerOptions)).Get<ServerOptions>();
                    options.Address = new Uri(serverOptions.GrpcAddress);

                }).ConfigurePrimaryHttpMessageHandler((sp) =>
                {
                    var httpClientHandler = new HttpClientHandler()
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };

                    if (OperatingSystem.IsWindows())
                    {
                        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 0, 0))
                        {
                            return httpClientHandler;
                        }
                        return new GrpcWebHandler(httpClientHandler);
                    }

                    return httpClientHandler;

                }).ConfigureChannel((sp,options) =>
                {
                    options.Credentials = ChannelCredentials.SecureSsl;
                    options.ServiceProvider = sp;
                    var defaultMethodConfig = new MethodConfig
                    {
                        Names = { MethodName.Default },
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = 100,
                            InitialBackoff = TimeSpan.FromSeconds(1),
                            MaxBackoff = TimeSpan.FromSeconds(10),
                            BackoffMultiplier = 1.5,
                            RetryableStatusCodes = { StatusCode.Unavailable }
                        }
                    };
                    options.ServiceConfig = new ServiceConfig()
                    {
                        MethodConfigs = { defaultMethodConfig }
                    };
                });

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
                var env = Environment.GetEnvironmentVariable("NodeServiceEnvironments", EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrEmpty(env))
                {
                    options.env = env;
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
