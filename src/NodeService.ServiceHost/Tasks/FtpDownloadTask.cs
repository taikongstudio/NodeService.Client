using FluentFTP;

namespace NodeService.ServiceHost.Tasks
{
    public class FtpDownloadTask : TaskBase, IProgress<FtpProgress>
    {
        private readonly INodeIdentityProvider _nodeIdentityProvider;

        public FtpDownloadTask(
            INodeIdentityProvider nodeIdentityProvider,
            ApiService apiService,
            ILogger<TaskBase> logger) : base(apiService, logger)
        {
            _nodeIdentityProvider = nodeIdentityProvider;
        }



        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var options = new FtpDownloadTaskOptions();
            await options.InitAsync(TaskDefinition, ApiService, cancellationToken);
            var nodeId = _nodeIdentityProvider.GetIdentity();
            await ApplyEnvVarsAsync(nodeId, options.FtpDownloadConfig, cancellationToken);
            var ftpDownloadConfigExecutor = new FtpDownloadConfigExecutor(this, options.FtpDownloadConfig, Logger);
            await ftpDownloadConfigExecutor.ExecuteAsync(cancellationToken);
        }

        async ValueTask ApplyEnvVarsAsync(string nodeId, FtpDownloadConfigModel ftpDownloadConfig, CancellationToken cancellationToken)
        {
            var rsp = await ApiService.QueryNodeEnvVarsConfigAsync(nodeId, cancellationToken);
            if (rsp.ErrorCode == 0 && rsp.Result != null)
            {
                foreach (var envVar in rsp.Result.Value.EnvironmentVariables)
                {
                    ftpDownloadConfig.LocalDirectory = ftpDownloadConfig.LocalDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    ftpDownloadConfig.RemoteDirectory = ftpDownloadConfig.RemoteDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    ftpDownloadConfig.SearchPattern = ftpDownloadConfig.SearchPattern.Replace($"$({envVar.Name})", envVar.Value);
                }
            }
        }

        public void Report(FtpProgress value)
        {

        }
    }
}
