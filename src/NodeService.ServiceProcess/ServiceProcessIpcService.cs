namespace NodeService.ServiceProcess
{
    public class ServiceProcessIpcService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}
