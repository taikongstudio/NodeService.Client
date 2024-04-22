namespace NodeService.ServiceProcess
{
    public class ProcessExitService : BackgroundService
    {
        private readonly ILogger<ProcessExitService> _logger;

        public ProcessExitService(ILogger<ProcessExitService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var exitFileName = Path.Combine(AppContext.BaseDirectory, "exit.txt");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    if (File.Exists(exitFileName))
                    {
                        File.Delete(exitFileName);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }
            Environment.Exit(0);
        }

    }
}
