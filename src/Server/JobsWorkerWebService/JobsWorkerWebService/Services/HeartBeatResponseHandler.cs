using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Data;
using JobsWorkerWebService.Extensions;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace JobsWorkerWebService.Services
{
    public interface IHeartBeatResponseHandler : IAsyncDisposable
    {
        Task HandleAsync(HttpContext httpContext, HeartBeatResponse heartBeatResponse);
    }

    public class HeartBeatResponseHandler : IHeartBeatResponseHandler, IAsyncDisposable
    {
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<HeartBeatResponseHandler> _logger;

        public HeartBeatResponseHandler(
            ApplicationDbContext applicationDbContext,
            IMemoryCache memoryCache,
            ILogger<HeartBeatResponseHandler> logger)
        {
            this._applicationDbContext = applicationDbContext;
            this._memoryCache = memoryCache;
            this._logger = logger;
        }

        public NodeInfo NodeInfo { get; private set; }

        public  ValueTask DisposeAsync()
        {
            if (this.NodeInfo != null)
            {
                this.NodeInfo.is_online = false;
            }
            return ValueTask.CompletedTask;
        }

        public async Task HandleAsync(HttpContext httpContext, HeartBeatResponse heartBeatResponse)
        {
            try
            {
                heartBeatResponse.Content.Properties.TryAdd(NodeProperties.NodeName_Key, heartBeatResponse.Content.NodeName);
                var remoteIPAddress = httpContext.Connection.RemoteIpAddress;
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(heartBeatResponse.Content.NodeName);
                if (nodeInfo == null)
                {
                    nodeInfo = NodeInfo.Create(heartBeatResponse.Content.NodeName);
                    this._applicationDbContext.NodeInfoDbSet.Add(nodeInfo);
                }
                this.NodeInfo = nodeInfo;
                nodeInfo.is_online = true;
                nodeInfo.update_time = heartBeatResponse.Content.Properties[NodeProperties.LastUpdateDateTime_Key];
                nodeInfo.version = heartBeatResponse.Content.Properties[NodeProperties.Version_Key];
                nodeInfo.ip_addresses = remoteIPAddress.ToString();
                nodeInfo.install_status = true;
                nodeInfo.login_name = heartBeatResponse.Content.Properties[NodeProperties.Environment_UserName_Key];
                if (nodeInfo.ip_addresses.StartsWith("10"))
                {
                    nodeInfo.factory_name = "BL";
                }
                else if (nodeInfo.ip_addresses.StartsWith("172"))
                {
                    nodeInfo.factory_name = "GM";
                }
                else
                {
                    nodeInfo.factory_name = "Unknown";
                }
                ConcurrentDictionary<string, string>? propDict = await this._memoryCache.GetOrCreateNodePropsAsync(heartBeatResponse.Content.NodeName);
                foreach (var item in heartBeatResponse.Content.Properties)
                {
                    if (item.Key == null)
                    {
                        continue;
                    }
                    propDict.AddOrUpdate(item.Key, item.Value, (key, oldValue) => item.Value);
                }
                await this._applicationDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }

        }

    }
}
