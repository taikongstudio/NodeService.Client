using Microsoft.Extensions.DependencyInjection;
using NodeService.Infrastructure.Concurrent;
using NodeService.ServiceHost.Models;
using NodeService.ServiceHost.Tasks;
using Type = System.Type;

namespace NodeService.ServiceHost.Services
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
                var taskExecutionContext = await _taskExecutionContextQueue.DeuqueAsync(stoppingToken);
                StartTaskExecutionContextService(taskExecutionContext);
            }
        }

        private void StartTaskExecutionContextService(TaskExecutionContext taskExecutionContext)
        {
            var thread = new Thread(ExecuteTaskImpl);
            thread.Start(taskExecutionContext);
        }

        private bool IsTaskType(Type type)
        {
            if (type == typeof(TaskBase))
            {
                return false;
            }
            return type.IsAssignableTo(typeof(TaskBase));
        }

        private void SetupHttpClient(HttpClient httpClient)
        {
            httpClient.Timeout = TimeSpan.FromHours(1);
            httpClient.BaseAddress = new Uri(_serverOptions.HttpAddress);
        }

        private async void ExecuteTaskImpl(object? state)
        {
            try
            {
                if (state is not TaskExecutionContext taskExecutionContext)
                {
                    return;
                }
                try
                {

                    var builder = Host.CreateApplicationBuilder([]);
                    builder.Services.AddSingleton(_serverOptions);
                    builder.Services.AddSingleton(taskExecutionContext);
                    builder.Services.AddHostedService<TaskExecutionService>();
                    builder.Services.AddSingleton(_nodeIdentityProvider);
                    builder.Services.AddScoped<ApiService>();
                    foreach (var taskType in typeof(TaskBase).Assembly.GetExportedTypes().Where(IsTaskType))
                    {
                        builder.Services.AddScoped(taskType);
                    }
                    builder.Services.AddSingleton(taskExecutionContext.LogMessageTargetBlock);
                    builder.Services.AddSingleton(_taskExecutionContextDictionary);
                    builder.Services.AddScoped(sp => sp.GetService<ILoggerFactory>().CreateLogger("TaskLogger")
                    );
                    { }
                    builder.Services.AddHttpClient(Options.DefaultName, SetupHttpClient).RemoveAllLoggers().AddDefaultLogger();
                    builder.Logging.ClearProviders();
                    builder.Logging.AddConsole();
                    builder.Logging.AddFilter((category, lever) =>
                    {
                        if (category == "Microsoft.Hosting.Lifetime")
                        {
                            return false;
                        }
                        return true;
                    });
                    builder.Logging.AddTaskLogger();

                    using var app = builder.Build();

                    await app.RunAsync(taskExecutionContext.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    await taskExecutionContext.UpdateStatusAsync(TaskExecutionStatus.Failed, ex.ToString());
                }
                finally
                {
                    if (!taskExecutionContext.IsDisposed)
                    {
                        await taskExecutionContext.DisposeAsync();
                    }
                    _taskExecutionContextDictionary.TryRemove(taskExecutionContext.Parameters.Id, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }
    }
}
