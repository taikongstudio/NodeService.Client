using MaccorUploadTool;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MaccorUploadTool.Services
{
    public class ProcessService : Microsoft.Extensions.Hosting.BackgroundService
    {
        private ILogger<ProcessService> _logger;
        private readonly Options _options;

        public ProcessService(ILogger<ProcessService> logger, Options options)
        {
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!int.TryParse(_options.ParentProcessId, out var parentProcessId))
            {
                _logger.LogInformation("invalid parent process id");
                Console.Out.Flush();
                Environment.Exit(0);
                return;
            }
            var parentProcess = Process.GetProcessById(parentProcessId);
            var exitFileName = Path.Combine(AppContext.BaseDirectory, "exit.txt");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(exitFileName))
                    {
                        File.Delete(exitFileName);
                        break;
                    }
                    if (parentProcess.HasExited)
                    {
                        Console.Out.Flush();
                        Environment.Exit(0);
                    }
                    await Task.Delay(5000);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex.ToString());
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    break;
                }

            }
            Environment.Exit(0);
        }

    }
}
