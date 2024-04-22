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
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
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
                foreach (var xmlPath in Directory.GetFiles(tasksDirectory, "RegisterTask*",
                    new EnumerationOptions()
                    {
                        RecurseSubdirectories = true,
                        MatchCasing = MatchCasing.PlatformDefault,
                    }
                    ))
                {
                    var taskName = Path.GetFileNameWithoutExtension(xmlPath);
                    using var taskDefinition = Microsoft.Win32.TaskScheduler.TaskService.Instance.NewTaskFromFile(xmlPath);
                    var task = folder.Tasks.FirstOrDefault(x => x.Name == taskName);
                    if (task == null)
                    {
                        task = folder.RegisterTaskDefinition(taskName, taskDefinition);
                    }
                    if (task.Definition.XmlText != taskDefinition.XmlText)
                    {
                        task.Definition.XmlText = taskDefinition.XmlText;
                        task.RegisterChanges();
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
