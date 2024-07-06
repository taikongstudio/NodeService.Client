using FluentFTP;
using System.Collections.Concurrent;
using System.Net.Http;
namespace NodeService.ServiceHost.Tasks
{
    public class FtpUploadTask : TaskBase, IProgress<FtpProgress>
    {
        readonly INodeIdentityProvider _nodeIdentityProvider;
        readonly IHttpClientFactory _httpClientFactory;
        readonly ConcurrentDictionary<string, FtpProgress> _progressDict;

        public FtpUploadTask(
            INodeIdentityProvider nodeIdentityProvider,
            IHttpClientFactory httpClientFactory,
            ApiService apiService,
            ILogger<FtpUploadTask> logger) : base(apiService, logger)
        {
            _nodeIdentityProvider = nodeIdentityProvider;
            _httpClientFactory = httpClientFactory;
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
                var httpUploadTaskExecutor = new HttpUploadExecutor(
                    nodeId,
                    ftpUploadConfig,
                    _httpClientFactory,
                    Logger);
                await ApplyNodeEnvVarsAsync(
                    nodeId,
                    ftpUploadConfig,
                    cancellationToken);
                httpUploadTaskExecutor.SetEnvironmentVariables(EnvironmentVariables);
                await httpUploadTaskExecutor.ExecuteAsync(cancellationToken);
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
