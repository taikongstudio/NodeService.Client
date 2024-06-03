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

        public override async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            FtpUploadJobOptions ftpUploadJobOptions = new FtpUploadJobOptions();
            await ftpUploadJobOptions.InitAsync(TaskScheduleConfig, ApiService);
            var nodeId = _nodeIdentityProvider.GetIdentity();
            foreach (var ftpUploadConfig in ftpUploadJobOptions.FtpUploadConfigs)
            {
                Logger.LogInformation($"Start executing {ftpUploadConfig.Name}");
                FtpUploadConfigExecutor ftpTaskExecutor = new FtpUploadConfigExecutor(this, ftpUploadConfig, Logger);
                await ApplyNodeEnvVarsAsync(nodeId, ftpUploadConfig, stoppingToken);
                ApplyTaskEnvVars(ftpUploadConfig);
                await ftpTaskExecutor.ExecuteAsync(stoppingToken);
                Logger.LogInformation($"Finish executing {ftpUploadConfig.Name} Completed");
            }
        }

        private void ApplyTaskEnvVars(FtpUploadConfigModel ftpUploadConfig)
        {
            foreach (var envVar in EnvironmentVariables)
            {
                switch (envVar.Key)
                {
                    case nameof(FtpUploadConfigModel.LocalDirectory):
                        ftpUploadConfig.LocalDirectory = envVar.Value;
                        break;
                    case nameof(FtpUploadConfigModel.RemoteDirectory):
                        ftpUploadConfig.RemoteDirectory = envVar.Value;
                        break;
                    default:
                        break;
                }
            }
        }

        private async Task ApplyNodeEnvVarsAsync(
            string nodeId,
            FtpUploadConfigModel ftpUploadConfig,
            CancellationToken stoppingToken = default)
        {
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
        }

        public void Report(FtpProgress value)
        {

        }
    }
}
