using JobsWorker.Shared.DataModels;
using System.Globalization;

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

        public string? NodeId { get; private set; }

        public async ValueTask DisposeAsync()
        {
            if (this.NodeId == null)
            {
                return;

            }
            var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(this.NodeId);
            if (nodeInfo != null)
            {
                nodeInfo.Status = NodeStatus.Offline;
                await this._applicationDbContext.SaveChangesAsync();
            }
        }

        internal static long GetLongTimeStamp(DateTime dateTime)
        {
            return (long)dateTime.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public async Task HandleAsync(HttpContext httpContext, HeartBeatResponse heartBeatResponse)
        {
            try
            {
                string nodeName = heartBeatResponse.Content.NodeName;
                var remoteIPAddress = httpContext.Connection.RemoteIpAddress;
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FirstOrDefaultAsync(x => x.Name == nodeName);
                if (nodeInfo == null)
                {
                    nodeInfo = NodeInfoModel.Create(nodeName);
                    var defaultNodeConfigTemplate = await this._applicationDbContext.NodeConfigTemplateDbSet.FirstOrDefaultAsync(x => x.IsDefault);
                    if (defaultNodeConfigTemplate != null)
                    {
                        nodeInfo.ActiveNodeConfigTemplateForeignKey = defaultNodeConfigTemplate.Id;
                    }
                    this._applicationDbContext.NodeInfoDbSet.Add(nodeInfo);
                }
                this.NodeId = nodeInfo.Id;
                await this._applicationDbContext.LoadAsync(nodeInfo);
                nodeInfo.Status =  NodeStatus.Online;
                nodeInfo.Profile.UpdateTime =
                    DateTime.ParseExact(heartBeatResponse.Content.Properties[NodePropertyModel.LastUpdateDateTime_Key],
                    NodePropertyModel.DateTimeFormatString, DateTimeFormatInfo.InvariantInfo);
                nodeInfo.Profile.ClientVersion = heartBeatResponse.Content.Properties[NodePropertyModel.ClientVersion_Key];
                nodeInfo.Profile.IpAddress = remoteIPAddress.ToString();
                nodeInfo.Profile.InstallStatus = true;
                nodeInfo.Profile.LoginName = heartBeatResponse.Content.Properties[NodePropertyModel.Environment_UserName_Key];
                nodeInfo.Profile.FactoryName = heartBeatResponse.Content.Properties[NodePropertyModel.FactoryName_key];

                ConcurrentDictionary<string, string>? propsDict = await this._memoryCache.GetOrCreateNodePropsAsync(heartBeatResponse.Content.NodeName);


                var expireDate = DateTime.Now - TimeSpan.FromDays(7);
                this._applicationDbContext.NodePropsDbSet.RemoveRange(this._applicationDbContext.NodePropsDbSet.Where(x => x.CreationDateTime < expireDate));
                foreach (var item in heartBeatResponse.Content.Properties)
                {
                    if (item.Key == null)
                    {
                        continue;
                    }
                    propsDict.AddOrUpdate(item.Key, item.Value, (key, oldValue) => item.Value);
                }

                if (propsDict.Count > 0)
                {
                    var nodePropertySnapshotModel =
                        new NodePropertySnapshotModel()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = "Default",
                            CreationDateTime = nodeInfo.Profile.UpdateTime,
                            NodeProperties = propsDict.Select(x => new NodePropertyEntry(x.Key, x.Value)).ToList(),
                            Group = nodeInfo.PropertySnapshotGroups.FirstOrDefault()
                        };
                    this._applicationDbContext.NodePropsDbSet.Add(nodePropertySnapshotModel);
                    nodeInfo.LastNodePropertySnapshot = nodePropertySnapshotModel;
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
