using FluentFTP;

namespace NodeService.ServiceHost.Tasks
{
    public class FtpDownloadJob : TaskBase, IProgress<FtpProgress>
    {
        private readonly INodeIdentityProvider _nodeIdentityProvider;

        public FtpDownloadJob(
            INodeIdentityProvider nodeIdentityProvider,
            ApiService apiService,
            ILogger<TaskBase> logger) : base(apiService, logger)
        {
            _nodeIdentityProvider = nodeIdentityProvider;
        }



        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            FtpDownloadJobOptions options = new FtpDownloadJobOptions();
            await options.InitAsync(TaskDefinition, ApiService, cancellationToken);
            var nodeId = _nodeIdentityProvider.GetIdentity();
            var rsp = await ApiService.QueryNodeEnvVarsConfigAsync(nodeId, cancellationToken);
            if (rsp.ErrorCode == 0 && rsp.Result != null)
            {
                foreach (var envVar in rsp.Result.Value.EnvironmentVariables)
                {
                    options.FtpDownloadConfig.LocalDirectory = options.FtpDownloadConfig.LocalDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    options.FtpDownloadConfig.RemoteDirectory = options.FtpDownloadConfig.RemoteDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    options.FtpDownloadConfig.SearchPattern = options.FtpDownloadConfig.SearchPattern.Replace($"$({envVar.Name})", envVar.Value);
                }
            }
            FtpDownloadConfigExecutor ftpDownloadConfigExecutor = new FtpDownloadConfigExecutor(this, options.FtpDownloadConfig, Logger);
            await ftpDownloadConfigExecutor.ExecuteAsync(cancellationToken);

        }

        public void Report(FtpProgress value)
        {

        }
    }
}
