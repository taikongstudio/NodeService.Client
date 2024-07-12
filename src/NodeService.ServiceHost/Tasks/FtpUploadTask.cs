using FluentFTP;
using NodeService.Infrastructure;
using NodeService.ServiceHost.Models;
using System.Collections.Concurrent;
using System.Net.Http;
namespace NodeService.ServiceHost.Tasks
{
    public class FtpUploadTask : TaskBase
    {
        readonly ServerOptions _serverOptions;
        readonly INodeIdentityProvider _nodeIdentityProvider;
        readonly IHttpClientFactory _httpClientFactory;
        readonly ConcurrentDictionary<string, FtpProgress> _progressDict;

        public FtpUploadTask(
            INodeIdentityProvider nodeIdentityProvider,
            IHttpClientFactory httpClientFactory,
            ApiService apiService,
            ServerOptions serverOptions,
            ILogger<FtpUploadTask> logger) : base(apiService, logger)
        {
            _serverOptions = serverOptions;
            _nodeIdentityProvider = nodeIdentityProvider;
            _httpClientFactory = httpClientFactory;
            _progressDict = new ConcurrentDictionary<string, FtpProgress>();
        }

        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var ftpUploadTaskOptions = new FtpUploadTaskOptions();
            await ftpUploadTaskOptions.InitAsync(TaskDefinition, ApiService);
            var nodeId = _nodeIdentityProvider.GetIdentity();
            var queryNodeInfoRsp = await ApiService.QueryNodeInfoAsync(nodeId, cancellationToken);

            if (queryNodeInfoRsp.Result == null)
            {
                throw new Exception("Node info not found");
            }
            var nodeInfo = queryNodeInfoRsp.Result;
            var queryNodeSettingsRsp = await ApiService.QueryNodeSettingsAsync(cancellationToken);
            if (queryNodeSettingsRsp.Result==null)
            {
                throw new Exception("Node settings not found");
            }
            var nodeSettings = queryNodeSettingsRsp.Result;
            string? siteUrl = null;
            foreach (var item in nodeSettings.NodeSiteMapping)
            {
                if (item.Name==nodeInfo.Profile.FactoryName)
                {
                    siteUrl = item.Value;
                    break;
                }
            }
            if (siteUrl==null)
            {
                throw new Exception("site url not found");
            }
            foreach (var ftpUploadConfig in ftpUploadTaskOptions.FtpUploadConfigs)
            {
                Logger.LogInformation($"Start executing config:{ftpUploadConfig.Name}");
                var httpUploadTaskExecutor = new HttpUploadExecutor(
                    nodeId,
                    TaskCreationParameters.Id,
                    siteUrl,
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
        }


        async Task ApplyNodeEnvVarsAsync(
                    string nodeId,
                    FtpUploadConfigModel ftpUploadConfig,
                    CancellationToken cancellationToken = default)
        {
            var rsp = await ApiService.QueryNodeEnvVarsConfigAsync(nodeId, cancellationToken);
            if (rsp.ErrorCode == 0 && rsp.Result != null)
            {
                foreach (var envVar in rsp.Result.EnvironmentVariables)
                {
                    ftpUploadConfig.LocalDirectory = ftpUploadConfig.LocalDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    ftpUploadConfig.RemoteDirectory = ftpUploadConfig.RemoteDirectory.Replace($"$({envVar.Name})", envVar.Value);
                    if (ftpUploadConfig.SearchPattern != null)
                    {
                        ftpUploadConfig.SearchPattern = ftpUploadConfig.SearchPattern.Replace($"$({envVar.Name})", envVar.Value);
                    }
                }
            }
        }

    }
}
