using NodeService.WindowsService.Models;

namespace NodeService.WindowsService.Services
{
    public class RegisterTaskService : BackgroundService
    {
        private readonly ILogger<RegisterTaskService> _logger;
        private ServiceOptions _serviceOptions;

        public RegisterTaskService(
            ServiceOptions  serviceOptions,
            ILogger<RegisterTaskService> logger)
        {
            _logger = logger;
            _serviceOptions = serviceOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                RegisterStartup();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

        }

        private void RegisterStartup()
        {
            try
            {
                var folder = Microsoft.Win32.TaskScheduler.TaskService.Instance.GetFolder("NodeService.Tasks");
                if (folder == null)
                {
                    folder = Microsoft.Win32.TaskScheduler.TaskService.Instance.RootFolder.CreateFolder("NodeService.Tasks");
                }
                var tasksDirectory = Path.Combine(AppContext.BaseDirectory, "Tasks");
                if (!Directory.Exists(tasksDirectory))
                {
                    Directory.CreateDirectory(tasksDirectory);
                }
                List<string> taskNames = new List<string>();
                foreach (var xmlPath in Directory.GetFiles(tasksDirectory, "*",
                    new EnumerationOptions()
                    {
                        RecurseSubdirectories = true,
                        MatchCasing = MatchCasing.PlatformDefault,
                    }
                    ))
                {
                    var taskName = Path.GetFileNameWithoutExtension(xmlPath);
                    taskNames.Add(taskName);
                    using var taskDefinition = Microsoft.Win32.TaskScheduler.TaskService.Instance.NewTaskFromFile(xmlPath);
                    var task = folder.Tasks.FirstOrDefault(x => x.Name == taskName);
                    if (task == null)
                    {
                        task = folder.RegisterTaskDefinition(taskName, taskDefinition);
                        _logger.LogInformation($"Register task {taskName}");
                        task.Dispose();
                    }
                }
                foreach (var task in folder.Tasks.ToArray())
                {
                    if (!taskNames.Exists(x => x == task.Name))
                    {
                        folder.DeleteTask(task.Name, false);
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
