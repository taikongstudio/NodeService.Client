using NLog;
using System.Diagnostics;

namespace JobsWorkerNodeService.Services
{
    public class ProcessService : BackgroundService
    {
        private ILogger<ProcessService> _logger;
        private readonly Options _options;

        public ProcessService(ILogger<ProcessService> logger, Options options)
        {
            this._logger = logger;
            this._options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var exitFileName = Path.Combine(AppContext.BaseDirectory, "exit.txt");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000);
                    if (File.Exists(exitFileName))
                    {
                        File.Delete(exitFileName);
                        break;
                    }
                    if (!Debugger.IsAttached)
                    {
                        DetectParentProcess();
                    }
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex.ToString());
                    break;
                }

            }
            Environment.Exit(0);
        }

        private void DetectParentProcess()
        {
            if (int.TryParse(this._options.parentprocessid, out var processId))
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    if (process == null || process.HasExited)
                    {
                        this._logger.LogError($"exit process null process or exited process");
                    }
                    else
                    {
                        return;
                    }
                }
                catch (ArgumentException ex)
                {
                    this._logger.LogError(ex.ToString());
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex.ToString());
                    return;
                }

            }

            this._logger.LogError($"exit process");
            LogManager.Flush();
            LogManager.Shutdown();
            Environment.Exit(0);
        }

    }
}
