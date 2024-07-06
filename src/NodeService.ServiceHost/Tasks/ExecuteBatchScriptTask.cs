namespace NodeService.ServiceHost.Tasks
{
    public class ExecuteBatchScriptTask : TaskBase
    {
        private readonly INodeIdentityProvider _nodeIdentityProvider;

        public ExecuteBatchScriptTask(
            INodeIdentityProvider nodeIdentityProvider,
            ApiService apiService,
            ILogger<TaskBase> logger) : base(apiService, logger)
        {
            _nodeIdentityProvider = nodeIdentityProvider;
        }

        private void WriteOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }
            Logger.LogInformation(e.Data);
        }

        private void WriteError(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }
            Logger.LogError(e.Data);
        }

        private string EnsureScriptsHomeDirectory()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "scripts");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            string batchScriptTempFile = null;
            using var process = new Process();
            try
            {
                string nodeId = this._nodeIdentityProvider.GetIdentity();
                batchScriptTempFile = Path.Combine(EnsureScriptsHomeDirectory(), $"{Guid.NewGuid()}.bat");
                ExecuteBatchScriptJobOptions options = new ExecuteBatchScriptJobOptions();
                await options.InitAsync(TaskDefinition, ApiService);
                string scripts = options.Scripts.Replace("$(WorkingDirectory)", AppContext.BaseDirectory);
                scripts = scripts.ReplaceLineEndings("\r\n");
                scripts = scripts.Replace("$(NodeId)", nodeId);
                scripts = scripts.Replace("$(HostName)", Dns.GetHostName());
                scripts = scripts.Replace("$(ParentProcessId)", Environment.ProcessId.ToString());
                var rsp = await ApiService.QueryNodeEnvVarsConfigAsync(nodeId, cancellationToken);
                if (rsp.ErrorCode == 0 && rsp.Result != null)
                {
                    foreach (var envVar in rsp.Result.Value.EnvironmentVariables)
                    {
                        scripts = scripts.Replace($"$({envVar.Name})", envVar.Value);
                    }
                }
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
                int taskIndex = Task.WaitAny(process.WaitForExitAsync(), Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
                if (taskIndex == 1)
                {
                    process.Kill(true);
                    Logger.LogInformation($"Kill process:{process.Id}");
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
            var cmdPath = $"{Environment.GetEnvironmentVariable("SystemRoot")}\\system32\\cmd.exe";
            if (!File.Exists(cmdPath))
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    cmdPath = Path.Combine(drive.RootDirectory.FullName, "\\windows\\system32\\cmd.exe");
                    if (File.Exists(cmdPath))
                    {
                        break;
                    }
                }

            }
            return cmdPath;
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
