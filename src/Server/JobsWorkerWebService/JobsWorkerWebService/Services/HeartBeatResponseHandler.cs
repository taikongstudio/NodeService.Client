using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Data;
using JobsWorkerWebService.Extensions;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace JobsWorkerWebService.Services
{
    public interface IHeartBeatResponseHandler : IDisposable
    {
        Task HandleAsync(HttpContext httpContext, HeartBeatResponse heartBeatResponse);
    }

    public class HeartBeatResponseHandler : IHeartBeatResponseHandler, IDisposable
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

        public void Dispose()
        {
            this._applicationDbContext.Dispose();
        }

        public async Task HandleAsync(HttpContext httpContext, HeartBeatResponse heartBeatResponse)
        {
            try
            {
                heartBeatResponse.Content.Properties.TryAdd(NodeProperties.NodeNameKey, heartBeatResponse.Content.NodeName);
                var remoteIPAddress = httpContext.Connection.RemoteIpAddress;
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(heartBeatResponse.Content.NodeName);
                if (nodeInfo == null)
                {
                    nodeInfo = new NodeInfo()
                    {
                        node_id = Guid.NewGuid().ToString("N"),
                        node_name = heartBeatResponse.Content.NodeName,
                    };
                    this._applicationDbContext.NodeInfoDbSet.Add(nodeInfo);
                }
                nodeInfo.update_time = heartBeatResponse.Content.Properties[NodeProperties.LastUpdateDateTimeKey];
                nodeInfo.version = heartBeatResponse.Content.Properties[NodeProperties.VersionKey];
                nodeInfo.ip_addresses = remoteIPAddress.ToString();
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
