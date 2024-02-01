
using JobsWorkerDaemonService.Helpers;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerDaemonService.Jobs
{
    public class ExecuteProcessJob : JobBase
    {
        public ExecuteProcessJob()
        {

        }

        public override async Task Execute(IJobExecutionContext context)
        {


            try
            {
                string exePath = this.Arguments["exePath"];

                var workDir = Path.GetDirectoryName(exePath);
                if (ProcessHelper.StartProcessAsCurrentUser(exePath, Logger, out var processId, null, workDir, true))
                {
                    var process = Process.GetProcessById((int)processId);
                    var outputDataRecieveEventHandler = new DataReceivedEventHandler((s, de) =>
                    {
                        this.Logger.LogInformation(de.Data);
                    });
                    process.OutputDataReceived += outputDataRecieveEventHandler;
                    process.BeginOutputReadLine();
                    process.WaitForExit();
                    process.OutputDataReceived -= outputDataRecieveEventHandler;
                    Logger.LogInformation($"start process:{process.Id}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

    }
}
