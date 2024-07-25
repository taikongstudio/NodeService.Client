using Microsoft.Extensions.Options;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using NodeService.WindowsService.Models;

namespace NodeService.WindowsService.Services
{
    public class RegisterTaskService : BackgroundService
    {
        private readonly ILogger<RegisterTaskService> _logger;
        private readonly ServiceOptions _serviceOptions;
        private readonly IOptionsMonitor<ServerOptions> _serverOptionsMonitor;
        private readonly IDisposable? _serverOptionsMonitorToken;
        private ServerOptions _serverOptions;
        private ApiService _apiService;

        public RegisterTaskService(
            ServiceOptions serviceOptions,
            ILogger<RegisterTaskService> logger,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor)
        {
            _logger = logger;
            _serviceOptions = serviceOptions;
            _serverOptionsMonitor = serverOptionsMonitor;
            _serverOptions = _serverOptionsMonitor.CurrentValue;
            OnServerOptionsChanged(_serverOptions);
            _serverOptionsMonitorToken = _serverOptionsMonitor.OnChange(OnServerOptionsChanged);
        }

        private void OnServerOptionsChanged(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
            _apiService?.Dispose();
            _apiService = new ApiService(new HttpClient()
            {
                BaseAddress = new Uri(_serverOptions.HttpAddress)
            });
        }

        public override void Dispose()
        {
            if (_serverOptionsMonitorToken != null)
            {
                _serverOptionsMonitorToken.Dispose();
            }
            base.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RegisterTaskAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }

        }

        private async Task<IEnumerable<WindowsTaskConfigModel>> QueryWindowsTasksConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation($"查询任务配置");
                var rsp = await _apiService.QueryWindowsTaskConfigListAsync(
                    new PaginationQueryParameters() { PageIndex = -1, PageSize = -1 },
                    cancellationToken);
                if (rsp.ErrorCode == 0)
                {
                    return rsp.Result ?? [];
                }
                else
                {
                    _logger.LogInformation($"查询任务配置失败:{rsp.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"查询任务配置失败:{ex.Message}");
            }
            return [];
        }

        private async Task RegisterTaskAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var folder = Microsoft.Win32.TaskScheduler.TaskService.Instance.GetFolder("NodeService.Tasks");
                if (folder == null)
                {
                    folder = Microsoft.Win32.TaskScheduler.TaskService.Instance.RootFolder.CreateFolder("NodeService.Tasks");
                }
                var windowsTaskConfigList = await QueryWindowsTasksConfigAsync(cancellationToken);
                if (!windowsTaskConfigList.Any())
                {
                    _logger.LogInformation($"使用默认任务配置");
                    using var stream = typeof(RegisterTaskService).Assembly.GetManifestResourceStream("NodeService.WindowsService.Tasks.RegisterTaskStartupService.xml");
                    using var streamReader = new StreamReader(stream, leaveOpen: true);
                    var defaultWindowsTaskConfig = new WindowsTaskConfigModel()
                    {
                        Name = "RegisterTaskStartupService",
                        XmlText = await streamReader.ReadToEndAsync(cancellationToken)
                    };
                    windowsTaskConfigList = [defaultWindowsTaskConfig];
                }
                var tasksDirectory = Path.Combine(AppContext.BaseDirectory, "Tasks");
                if (!Directory.Exists(tasksDirectory))
                {
                    Directory.CreateDirectory(tasksDirectory);
                }
                List<string> taskNames = [];
                foreach (var taskConfig in windowsTaskConfigList)
                {
                    var taskName = taskConfig.Name;
                    taskNames.Add(taskName);
                    var taskFilePath = Path.Combine(tasksDirectory, taskName);
                    await File.WriteAllTextAsync(taskFilePath, taskConfig.XmlText, cancellationToken);
                    _logger.LogInformation($"写入XmlText到路径 {taskFilePath}");
                    using var taskDefinition = Microsoft.Win32.TaskScheduler.TaskService.Instance.NewTaskFromFile(taskFilePath);
                    var task = folder.Tasks.FirstOrDefault(x => x.Name == taskName);
                    if (task == null)
                    {
                        task = folder.RegisterTaskDefinition(taskName, taskDefinition);
                        _logger.LogInformation($"Register task {taskName} Xml:{taskDefinition.XmlText}");
                        task.Dispose();
                    }
                    else if (task.Definition.RegistrationInfo.Date < taskDefinition.RegistrationInfo.Date)
                    {
                        folder.RegisterTask(taskName, taskDefinition.XmlText);
                        _logger.LogInformation($"Update task {taskName} Xml:{taskDefinition.XmlText}");
                    }
                }
                foreach (var task in folder.Tasks.ToArray())
                {
                    if (!taskNames.Exists(x => x == task.Name))
                    {
                        folder.DeleteTask(task.Name, false);
                        _logger.LogInformation($"Delete task {task.Name}");
                    }
                }
                folder.Dispose();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

    }
}
