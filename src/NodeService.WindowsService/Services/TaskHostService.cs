namespace NodeService.WindowsService.Services
{

    public class TaskHostService : BackgroundService
    {
        private readonly ILogger<TaskHostService> _logger;
        private readonly IAsyncQueue<TaskExecutionContext> _taskExecutionContextQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly TaskExecutionContextDictionary _taskExecutionContextDictionary;
        private readonly INodeIdentityProvider _nodeIdentityProvider;
        private readonly IDisposable? _serverOptionsMonitorToken;
        private ServerOptions _serverOptions;

        public override void Dispose()
        {
            if (_serverOptionsMonitorToken != null)
            {
                _serverOptionsMonitorToken.Dispose();

            }
            base.Dispose();
        }

        public TaskHostService(
            ILogger<TaskHostService> logger,
            IServiceProvider serviceProvider,
            IAsyncQueue<TaskExecutionContext> taskExecutionContextQueue,
            TaskExecutionContextDictionary taskExecutionContextDictionary,
            INodeIdentityProvider nodeIdentityProvider,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor
            )
        {
            _logger = logger;
            _taskExecutionContextQueue = taskExecutionContextQueue;
            _serviceProvider = serviceProvider;
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
            _nodeIdentityProvider = nodeIdentityProvider;
            _serverOptions = serverOptionsMonitor.CurrentValue;
            _serverOptionsMonitorToken = serverOptionsMonitor.OnChange(OnServerOptionsChanged);
        }

        private void OnServerOptionsChanged(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var jobExecutionContext = await _taskExecutionContextQueue.DeuqueAsync(stoppingToken);
                StartJobExecutionContextService(jobExecutionContext);
            }
        }

        private void StartJobExecutionContextService(TaskExecutionContext taskExecutionContext)
        {
            Task.Factory.StartNew(
                ExecuteTask,
                taskExecutionContext,
                taskExecutionContext.CancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void ExecuteTask(object? state)
        {
            TaskExecutionContext? taskExecutionContext = state as TaskExecutionContext;
            if (taskExecutionContext == null)
            {
                throw new InvalidOperationException(nameof(state));
            }
            try
            {

                HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
                builder.Services.AddSingleton(taskExecutionContext);
                builder.Services.AddHostedService<TaskExecutionService>();
                builder.Services.AddScoped<ExecuteBatchScriptJob>();
                builder.Services.AddScoped<ExecutePythonCodeJob>();
                builder.Services.AddScoped<ShouHuUploadJob>();
                builder.Services.AddScoped<FtpUploadJob>();
                builder.Services.AddScoped<LogUploadJob>();
                builder.Services.AddScoped<FtpDownloadJob>();
                builder.Services.AddSingleton(_nodeIdentityProvider);
                builder.Services.AddScoped(sp => new HttpClient
                {
                    BaseAddress = new Uri(_serverOptions.HttpAddress)
                });
                builder.Services.AddScoped<ApiService>();
                builder.Services.AddSingleton(taskExecutionContext.LogMessageTargetBlock);
                builder.Services.AddSingleton<TaskExecutionContextDictionary>(_taskExecutionContextDictionary);
                builder.Services.AddScoped(sp => sp.GetService<ILoggerFactory>().CreateLogger("JobLogger")
                );

                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.AddTaskLogger();



                using var app = builder.Build();

                app.RunAsync(taskExecutionContext.CancellationToken).Wait(taskExecutionContext.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                _taskExecutionContextDictionary.TryRemove(taskExecutionContext.Parameters.Id, out _);
            }
        }
    }
}
