using JobsWorkerNodeService.Jobs.Helpers;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs
{
    internal class DetectProcessJob : JobBase
    {
        public override async Task Execute(IJobExecutionContext context)
        {
            try
            {
                Logger.LogInformation("Start check");
                var processName = options["processName"];
                var exePath = options["exePath"];
                var processes = Process.GetProcessesByName(processName);
                PrintProcesses(processes);
                var process = processes.FirstOrDefault();


                if (process == null)
                {
                    Logger.LogInformation($"{processName}:process not found");
                    var workDir = Path.GetDirectoryName(exePath);
                    if (ProcessHelper.StartProcessAsCurrentUser(exePath, Logger, out var processId, null, workDir, true))
                    {
                        process = Process.GetProcessById((int)processId);
                        Logger.LogInformation($"start process:{process.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        private void PrintProcesses(Process[] processes)
        {
            foreach (var process in processes)
            {
                try
                {
                    Logger.LogInformation($"{process.ProcessName},{process.Id}");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }

            }
        }

    }
}
