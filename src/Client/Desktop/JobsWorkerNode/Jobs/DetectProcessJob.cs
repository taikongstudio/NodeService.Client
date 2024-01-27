using JobsWorkerNode.Helpers;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNode.Jobs
{
    internal class DetectProcessJob : JobBase
    {

        public Process Process { get; set; }

        public override async Task Execute(IJobExecutionContext context)
        {
            try
            {
                Logger.LogInformation("Start check");
                var processName = Arguments["processName"];
                var exePath = Arguments["exePath"];
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    Logger.LogInformation($"{process.ProcessName},{process.Id}");
                }


                Process = processes.FirstOrDefault();


                if (Process == null)
                {
                    Logger.LogInformation($"{processName}:process not found");
                    var workDir = Path.GetDirectoryName(exePath);
                    if (ProcessHelper.StartProcessAsCurrentUser(exePath, Logger, out var processId, null, workDir, true))
                    {
                        Process = Process.GetProcessById((int)processId);
                        Logger.LogInformation($"start process:{Process.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

    }
}
