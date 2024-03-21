using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.Daemon.Jobs
{
    public class ExecuteScriptJob : JobBase
    {
        public ExecuteScriptJob()
        {

        }

        public override async Task Execute(IJobExecutionContext context)
        {


            try
            {
                string batFilePath = Arguments["scripts"];
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "C:\\Windows\\System32\\cmd.exe";
                    process.StartInfo.Arguments = "/c " + batFilePath;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();

                    var outputDataRecieveEventHandler = new DataReceivedEventHandler((s, de) =>
                    {
                        Logger.LogInformation(de.Data);
                    });
                    process.OutputDataReceived += outputDataRecieveEventHandler;
                    process.BeginOutputReadLine();


                    // 等待脚本执行完毕
                    await process.WaitForExitAsync();

                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

    }
}
