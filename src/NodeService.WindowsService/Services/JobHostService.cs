﻿using NodeService.Infrastructure.NodeSessions;

namespace NodeService.WindowsService.Services
{

    public class JobHostService : BackgroundService

    {
        private readonly ILogger<JobHostService> _logger;
        private readonly IAsyncQueue<JobExecutionContext> _jobExecutionContextQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly JobContextDictionary _jobContextDictionary;
        private readonly INodeIdentityProvider _nodeIdentityProvider;

        public JobHostService(
            ILogger<JobHostService> logger,
            IServiceProvider serviceProvider,
            IAsyncQueue<JobExecutionContext> queue,
            JobContextDictionary jobContextDictionary,
            INodeIdentityProvider nodeIdentityProvider
            )
        {
            _logger = logger;
            _jobExecutionContextQueue = queue;
            _serviceProvider = serviceProvider;
            _jobContextDictionary = jobContextDictionary;
            _nodeIdentityProvider = nodeIdentityProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var jobExecutionContext = await _jobExecutionContextQueue.DeuqueAsync(stoppingToken);
                StartJobExecutionContextService(jobExecutionContext);
            }
        }

        private void StartJobExecutionContextService(JobExecutionContext jobExecutionContext)
        {
            var task = new Task(async () =>
            {
                try
                {
                    HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
                    builder.Services.AddSingleton<JobExecutionContext>(jobExecutionContext);
                    builder.Services.AddHostedService<JobExecutionService>();
                    builder.Services.AddScoped<ExecuteBatchScriptJob>();
                    builder.Services.AddScoped<ExecutePythonCodeJob>();
                    builder.Services.AddScoped<ShouHuUploadJob>();
                    builder.Services.AddScoped<FtpUploadJob>();
                    builder.Services.AddScoped<LogUploadJob>();
                    builder.Services.AddScoped<FtpDownloadJob>();
                    builder.Services.AddSingleton<INodeIdentityProvider>(_nodeIdentityProvider);
                    builder.Services.AddScoped(sp => new HttpClient
                    {
                        BaseAddress = new Uri(builder.Configuration.GetValue<string>("ServerConfig:HttpAddress"))
                    });
                    builder.Services.AddScoped<ApiService>();
                    builder.Services.AddSingleton<ITargetBlock<LogMessageEntry>>(jobExecutionContext.LogMessageTargetBlock);
                    builder.Services.AddSingleton<JobContextDictionary>(_jobContextDictionary);
                    builder.Services.AddScoped<ILogger>(sp => sp.GetService<ILoggerFactory>().CreateLogger("JobLogger")
                    );

                    builder.Logging.ClearProviders();
                    builder.Logging.AddConsole();
                    builder.Logging.AddJobServiceLogger();

                    

                    using var app = builder.Build();

                    await app.RunAsync(jobExecutionContext.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
                finally
                {
                    _jobContextDictionary.TryRemove(jobExecutionContext.Parameters.Id, out _);
                }





            }, jobExecutionContext.CancellationToken, TaskCreationOptions.LongRunning);
            task.Start();
        }
    }
}
