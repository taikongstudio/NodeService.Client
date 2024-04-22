using NodeService.ServiceHost.Models;
using NodeService.ServiceHost.Tasks;
using System.Linq;
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
                StartJobExecutionContextService(taskExecutionContext);
            }
        }

        private void StartJobExecutionContextService(TaskExecutionContext taskExecutionContext)
        {
            Task.Factory.StartNew(
                ExecuteTaskImpl,
                taskExecutionContext,
                taskExecutionContext.CancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private bool IsTaskType(Type type)
        {
            if (type == typeof(TaskBase))
            {
                return false;
            }
            return type.IsAssignableTo(typeof(TaskBase));
        }

        private void ExecuteTaskImpl(object? state)
        {
            if (state is not TaskExecutionContext taskExecutionContext)
            {
                return;
            }
            try
            {

                HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
                builder.Services.AddSingleton(taskExecutionContext);
                builder.Services.AddHostedService<TaskExecutionService>();
                builder.Services.AddSingleton(_nodeIdentityProvider);
                builder.Services.AddScoped(sp => new HttpClient
                {
                    BaseAddress = new Uri(_serverOptions.HttpAddress)
                });
                builder.Services.AddScoped<ApiService>();
                foreach (var taskType in typeof(TaskBase).Assembly.GetExportedTypes().Where(IsTaskType))
                {
                    builder.Services.AddScoped(taskType);
                }
                builder.Services.AddSingleton(taskExecutionContext.LogMessageTargetBlock);
                builder.Services.AddSingleton(_taskExecutionContextDictionary);
                builder.Services.AddScoped(sp => sp.GetService<ILoggerFactory>().CreateLogger("JobLogger")
                );

                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();
                builder.Logging.AddTaskLogger();



                using var app = builder.Build();

                app.RunAsync(taskExecutionContext.CancellationToken).Wait();
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
