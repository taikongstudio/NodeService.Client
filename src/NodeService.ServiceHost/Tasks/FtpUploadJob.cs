using FluentFTP;
using System.Collections.Concurrent;
namespace NodeService.ServiceHost.Tasks
{
    public class FtpUploadJob : TaskBase, IProgress<FtpProgress>
    {
        readonly INodeIdentityProvider _nodeIdentityProvider;
        readonly ConcurrentDictionary<string, FtpProgress> _progressDict;

        public FtpUploadJob(
            INodeIdentityProvider nodeIdentityProvider,
            ApiService apiService,
            ILogger<FtpUploadJob> logger) : base(apiService, logger)
        {
            _nodeIdentityProvider = nodeIdentityProvider;
            _progressDict = new ConcurrentDictionary<string, FtpProgress>();
        }

        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            FtpUploadJobOptions ftpUploadJobOptions = new FtpUploadJobOptions();
            await ftpUploadJobOptions.InitAsync(TaskDefinition, ApiService);
            var nodeId = _nodeIdentityProvider.GetIdentity();
            foreach (var ftpUploadConfig in ftpUploadJobOptions.FtpUploadConfigs)
            {
                Logger.LogInformation($"Start executing config:{ftpUploadConfig.Name}");
                var ftpUploadTaskExecutor = new FtpUploadConfigExecutor(this, ftpUploadConfig, Logger);
                await ApplyNodeEnvVarsAsync(nodeId, ftpUploadConfig, cancellationToken);
                ftpUploadTaskExecutor.SetEnvironmentVariables(EnvironmentVariables);
                await ftpUploadTaskExecutor.ExecuteAsync(cancellationToken);
                Logger.LogInformation($"Finish executing config:{ftpUploadConfig.Name} completed");
            }
            PrintStats();
        }

        private void PrintStats()
        {
            Logger.LogInformation("Progress:");
            foreach (var ftpProgress in _progressDict.Values)
            {
                if (ftpProgress == null)
                {
                    continue;
                }
                Logger.LogInformation($"LocalPath:{ftpProgress.LocalPath} RemotePath:{ftpProgress.RemotePath} Size:{ftpProgress.TransferredBytes} Time:{ftpProgress.ETA} TransferSpeed:{ftpProgress.TransferSpeedToString()}");
            }
        }

        public void Report(FtpProgress value)
        {
            _progressDict.AddOrUpdate(value.LocalPath, value, (key, oldValue) => value);
        }

        async Task ApplyNodeEnvVarsAsync(
                    string nodeId,
                    FtpUploadConfigModel ftpUploadConfig,
                    CancellationToken cancellationToken = default)
        {
            var rsp = await ApiService.QueryNodeEnvVarsConfigAsync(nodeId, cancellationToken);
            if (rsp.ErrorCode == 0 && rsp.Result != null)
            {
                foreach (var envVar in rsp.Result.Value.EnvironmentVariables)
                {
                    ftpUploadConfig.LocalDirectory = ftpUploadConfig.LocalDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    ftpUploadConfig.RemoteDirectory = ftpUploadConfig.RemoteDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    ftpUploadConfig.SearchPattern = ftpUploadConfig.SearchPattern.Replace($"$({envVar.Name})", envVar.Value);
                }
            }
        }

    }
}
