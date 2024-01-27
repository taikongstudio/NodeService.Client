using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;

namespace JobsWorkerNode.Jobs
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
                string scripts = Arguments["scripts"].Replace("$(WorkingDirectory)", AppContext.BaseDirectory);
                string workingDirectory = Arguments["workingDirectory"].Replace("$(WorkingDirectory)", AppContext.BaseDirectory);
                bool createNoWindow = bool.Parse(Arguments["createNoWindow"]);
                Logger.LogInformation($"Execute script :{scripts} at {workingDirectory} createNoWindow:{createNoWindow}");
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "C:\\Windows\\System32\\cmd.exe";
                    process.StartInfo.Arguments = "/c " + scripts;
                    process.StartInfo.WorkingDirectory = workingDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = createNoWindow;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();

                    var outputDataRecieveEventHandler = new DataReceivedEventHandler((s, de) =>
                    {
                        Logger.LogInformation(de.Data);
                    });

                    process.OutputDataReceived += outputDataRecieveEventHandler;
                    process.ErrorDataReceived += outputDataRecieveEventHandler;

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 等待脚本执行完毕
                    await process.WaitForExitAsync();
                    process.OutputDataReceived -= outputDataRecieveEventHandler;
                    process.ErrorDataReceived -= outputDataRecieveEventHandler;

                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

    }
}
