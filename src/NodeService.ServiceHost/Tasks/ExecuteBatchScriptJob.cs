using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.NodeSessions;
using System.Net;

namespace NodeService.ServiceHost.Tasks
{
    public class ExecuteBatchScriptJob : TaskBase
    {
        private readonly INodeIdentityProvider _nodeIdentityProvider;

        public ExecuteBatchScriptJob(
            INodeIdentityProvider nodeIdentityProvider,
            ApiService apiService,
            ILogger<TaskBase> logger) : base(apiService, logger)
        {
            _nodeIdentityProvider = nodeIdentityProvider;
        }

        private void WriteOutput(object sender, DataReceivedEventArgs e)
        {
            Logger.LogInformation(e.Data);
        }

        private void WriteError(object sender, DataReceivedEventArgs e)
        {
            Logger.LogError(e.Data);
        }

        private string EnsureScriptsHomeDirectory()
        {
            var path = "C:/shouhu/scripts";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            string batchScriptTempFile = null;
            using var process = new Process();
            try
            {
                batchScriptTempFile = Path.Combine(EnsureScriptsHomeDirectory(), $"{Guid.NewGuid()}.bat");
                ExecuteBatchScriptJobOptions options = new ExecuteBatchScriptJobOptions();
                await options.InitAsync(JobScheduleConfig, ApiService);
                string scripts = options.Scripts.Replace("$(WorkingDirectory)", AppContext.BaseDirectory);
                scripts = scripts.ReplaceLineEndings("\r\n");
                scripts = scripts.Replace("$(NodeId)", _nodeIdentityProvider.GetIdentity());
                scripts = scripts.Replace("$(HostName)", Dns.GetHostName());
                scripts = scripts.Replace("$(ParentProcessId)", Process.GetCurrentProcess().Id.ToString());
                string workingDirectory = options.WorkingDirectory.Replace("$(WorkingDirectory)", AppContext.BaseDirectory);
                bool createNoWindow = options.CreateNoWindow;

                File.WriteAllText(batchScriptTempFile, scripts);

                if (string.IsNullOrEmpty(workingDirectory))
                {
                    workingDirectory = AppContext.BaseDirectory;
                }


                string cmdFilePath = ResolveCmdExecutablePath();

                Logger.LogInformation($"{cmdFilePath} Execute scripts:{Environment.NewLine}{scripts}{Environment.NewLine} at {workingDirectory} createNoWindow:{createNoWindow}");

                process.StartInfo.FileName = cmdFilePath;
                process.StartInfo.Arguments = $"/c \"{batchScriptTempFile}\"";
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = options.CreateNoWindow;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                process.OutputDataReceived += WriteOutput;
                process.ErrorDataReceived += WriteError;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 等待脚本执行完毕
                int taskIndex = Task.WaitAny(process.WaitForExitAsync(), Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken));
                if (taskIndex == 1)
                {
                    process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            finally
            {
                process.OutputDataReceived -= WriteOutput;
                process.ErrorDataReceived -= WriteError;
                if (batchScriptTempFile != null)
                {
                    TryDeleteFile(batchScriptTempFile);
                }

            }
        }

        private static string ResolveCmdExecutablePath()
        {
            var cmdFilePath = $"{Environment.GetEnvironmentVariable("SystemRoot")}\\system32\\cmd.exe";
            if (!File.Exists(cmdFilePath))
            {
                cmdFilePath = "C:\\windows\\system32\\cmd.exe";
            }
            return cmdFilePath;
        }

        private void TryDeleteFile(string batchScriptTempFile)
        {
            try
            {
                File.Delete(batchScriptTempFile);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

    }
}
