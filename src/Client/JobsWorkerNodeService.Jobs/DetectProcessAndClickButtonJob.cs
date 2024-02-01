using JobsWorkerNodeService.Jobs.Helpers;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;
using System.Text.Json;

namespace JobsWorkerNodeService.Jobs
{
    internal class DetectProcessAndClickButtonJob : JobBase
    {

        public override async Task Execute(IJobExecutionContext context)
        {
            try
            {
                Logger.LogInformation("Start check");
                var processName = options["processName"];
                var exePath = options["exePath"];
                var processes = Process.GetProcessesByName(processName);
                var workingDirectory = options["workingDirectory"];
                foreach (var p in processes)
                {
                    Logger.LogInformation($"[Process]{p.ProcessName},{p.Id}");
                }

                var process = processes.FirstOrDefault();


                if (process == null)
                {
                    Logger.LogInformation($"{processName}:process not found");
                    if (!ProcessHelper.StartProcessAsCurrentUser(exePath, Logger, out var processId, null, workingDirectory, true))
                    {
                        Logger.LogInformation($"start process fail:{exePath}");
                        process = RunUIProcess(exePath, "", workingDirectory);
                        Logger.LogInformation($"start process default:{process.Id}");
                    }
                    else
                    {
                        process = Process.GetProcessById((int)processId);
                    }
                    Logger.LogInformation($"start process:{process.Id}");
                }

                string? buttonName = options["ButtonText"];
                int waitCount = 0;
                while (process.MainWindowHandle == nint.Zero)
                {
                    process.Refresh();
                    await Task.Delay(1000);
                    if (waitCount > 30)
                    {
                        break;
                    }
                    waitCount++;
                }

                int count = 0;
                do
                {
                    var hwndEnumProc = new HwndEnumProc((hwnd) =>
                    {
                        if (hwnd.Text.Trim().Contains(buttonName))
                        {
                            if (hwnd.IsEnabeld)
                            {
                                const int BM_CLICK = 0x00F5;
                                hwnd.SendMessage(BM_CLICK);
                                Logger.LogInformation($"Clicked {hwnd.Handle}");
                                return false;
                            }
                        }
                        return true;
                    });

                    process.EnumChildWindows(buttonName, hwndEnumProc);
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    count++;

                } while (count < 4);




            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }


        private Process RunUIProcess(string fileName, string arguments, string workingDirectory)
        {
            Process process = new Process();
            try
            {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.UseShellExecute = false;

                process.Start();


            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            return process;
        }


    }
}
