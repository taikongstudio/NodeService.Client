using JobsWorkerNodeService;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                while (true)
                {
                    try
                    {
                        using var process = Process.GetProcessById(processId);
                        if (process == null)
                        {
                            return;
                        }
                        try
                        {
                            if (process.HasExited)
                            {
                                goto LExit;
                            }
                        }
                        catch
                        {

                        }
                    }
                    catch (ArgumentException ex)
                    {
                        this._logger.LogError(ex.ToString());
                        goto LExit;

                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex.ToString());
                    }
                }

            }
        LExit:
            this._logger.LogError($"exit process");
            LogManager.Flush();
            LogManager.Shutdown();
            Environment.Exit(0);
        }

    }
}
