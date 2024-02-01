using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Data;
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

        public HeartBeatResponseHandler(
            ApplicationDbContext applicationDbContext,
            IMemoryCache memoryCache)
        {
            this._applicationDbContext = applicationDbContext;
            this._memoryCache = memoryCache;
        }

        public void Dispose()
        {
            this._applicationDbContext.Dispose();
        }

        public async Task HandleAsync(HttpContext httpContext, HeartBeatResponse heartBeatResponse)
        {
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
            nodeInfo.update_time = heartBeatResponse.Content.Properties["DateTime"];
            nodeInfo.version = heartBeatResponse.Content.Properties["Version"];
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
            ConcurrentDictionary<string, string>? propDict = await CacheNodePropsAsync(heartBeatResponse.Content.NodeName);
            foreach (var item in heartBeatResponse.Content.Properties)
            {
                propDict.AddOrUpdate(item.Key, item.Value, (key, oldValue) => item.Value);
            }
            await this._applicationDbContext.SaveChangesAsync();
        }

        private async Task<ConcurrentDictionary<string, string>?> CacheNodePropsAsync(string nodeName)
        {
            string key = $"NodeProps:{nodeName}";
            var propDict = await this._memoryCache.GetOrCreateAsync<ConcurrentDictionary<string, string>>(key, (cacheEntry) =>
            {
                var dict = new ConcurrentDictionary<string, string>();
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return Task.FromResult(dict);
            });
            return propDict;
        }
    }
}
