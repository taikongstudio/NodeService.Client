using FluentFTP;

namespace NodeService.ServiceHost.Tasks
{
    public class FtpUploadJob : TaskBase, IProgress<FtpProgress>
    {
        private readonly INodeIdentityProvider _nodeIdentityProvider;

        public FtpUploadJob(
            INodeIdentityProvider nodeIdentityProvider,
            ApiService apiService,
            ILogger<FtpUploadJob> logger) : base(apiService, logger)
        {
            _nodeIdentityProvider = nodeIdentityProvider;
        }

        private void ProcessFtpProgress(FtpProgress progress)
        {

        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            FtpUploadJobOptions ftpUploadJobOptions = new FtpUploadJobOptions();
            await ftpUploadJobOptions.InitAsync(TaskScheduleConfig, ApiService);
            var nodeId = _nodeIdentityProvider.GetIdentity();
            foreach (var ftpUploadConfig in ftpUploadJobOptions.FtpUploadConfigs)
            {
                Logger.LogInformation($"Start executing {ftpUploadConfig.Name}");
                FtpUploadConfigExecutor ftpTaskExecutor = new FtpUploadConfigExecutor(this, ftpUploadConfig, Logger);
                var rsp = await ApiService.QueryNodeEnvVarsConfigAsync(nodeId, stoppingToken);
                if (rsp.ErrorCode == 0 && rsp.Result != null)
                {
                    foreach (var envVar in rsp.Result.Value.EnvironmentVariables)
                    {
                        ftpUploadConfig.LocalDirectory = ftpUploadConfig.LocalDirectory.Replace($"$({envVar.Name})", envVar.Value);
                        ftpUploadConfig.RemoteDirectory = ftpUploadConfig.RemoteDirectory.Replace($"$({envVar.Name})", envVar.Value);
                        ftpUploadConfig.SearchPattern = ftpUploadConfig.SearchPattern.Replace($"$({envVar.Name})", envVar.Value);
                    }
                }
                await ftpTaskExecutor.ExecuteAsync(stoppingToken);
                Logger.LogInformation($"Finish executing {ftpUploadConfig.Name} Completed");
            }
        }

        public void Report(FtpProgress value)
        {

        }
    }
}
