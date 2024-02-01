using AntDesign.Charts;
using FluentFTP;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using JobsWorker.Shared;
using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Client;
using JobsWorkerWebService.Server.Data;
using JobsWorkerWebService.Server.Extensions;
using JobsWorkerWebService.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace JobsWorkerWebService.Server.GrpcServices
{
    public class JobsWorkerService : NodeService.NodeServiceBase
    {
        private readonly ILogger<JobsWorkerService> Logger;
        private readonly IInprocRpc<string, string, RequestMessage, ResponseMessage> _inprocRpc;
        private readonly IInprocMessageQueue<string, string, Message> _inprocMessageQueue;
        private readonly Dictionary<System.Type, Func<RequestMessage, SubscribeEvent, SubscribeEvent>> _builders;
        private readonly AsyncFtpClient _asyncFtpClient;
        private readonly FtpServerConfig _ftpServerConfig;
        private readonly ServerConfig _serverConfig;
        private readonly IMemoryCache _memoryCache;
        private readonly NodeInfoDbContext _nodeInfoDbContext;
        public JobsWorkerService(ILogger<JobsWorkerService> logger,
            IInprocRpc<string, string, RequestMessage, ResponseMessage> inprocRpc,
            IInprocMessageQueue<string, string, Message> inprocMessageQueue,
            AsyncFtpClient asyncFtpClient,
            FtpServerConfig ftpServerConfig,
            ServerConfig serverConfig,
            IMemoryCache memoryCache,
            NodeInfoDbContext nodeInfoDbContext
            )
        {
            this._memoryCache = memoryCache;
            this._serverConfig = serverConfig;
            this._asyncFtpClient = asyncFtpClient;
            this._ftpServerConfig = ftpServerConfig;
            this.Logger = logger;
            this._inprocRpc = inprocRpc;
            this._inprocMessageQueue = inprocMessageQueue;
            this._builders = new Dictionary<System.Type, Func<RequestMessage, SubscribeEvent, SubscribeEvent>>()
            {
                { typeof(HeartBeatRequest),BuildHeartBeatEvent},
                { typeof(FileSystemListDirectoryRequest),BuildFileSystemListDirectoryRequest},
                { typeof(FileSystemListDriveRequest),BuildFileSystemListDriveRequest},
                { typeof(FileSystemBulkOperationRequest),BuildFileSystemBulkOperationRequest}
            };
        }

        private SubscribeEvent BuildHeartBeatEvent(RequestMessage request, SubscribeEvent subscribeEvent)
        {
            subscribeEvent.Topic = "connectivity";
            subscribeEvent.HeartBeatReq = (request as RequestMessage<HeartBeatReq>).Content;
            return subscribeEvent;
        }

        private SubscribeEvent BuildFileSystemListDirectoryRequest(RequestMessage request, SubscribeEvent subscribeEvent)
        {
            subscribeEvent.Topic = "filesystem";
            subscribeEvent.FileSystemListDirectoryReq = (request as RequestMessage<FileSystemListDirectoryReq>).Content;
            return subscribeEvent;
        }

        private SubscribeEvent BuildFileSystemListDriveRequest(RequestMessage request, SubscribeEvent subscribeEvent)
        {
            subscribeEvent.Topic = "filesystem";
            subscribeEvent.FileSystemListDriveReq = (request as RequestMessage<FileSystemListDriveReq>).Content;
            return subscribeEvent;
        }

        private SubscribeEvent BuildFileSystemBulkOperationRequest(RequestMessage request, SubscribeEvent subscribeEvent)
        {
            subscribeEvent.Topic = "filesystem";
            subscribeEvent.FileSystemBulkOperationReq = (request as RequestMessage<FileSystemBulkOperationReq>).Content;
            return subscribeEvent;
        }

        public override async Task Subscribe(SubscribeRequest subscribeRequest, IServerStreamWriter<SubscribeEvent> responseStream, ServerCallContext context)
        {

            _ = Task.Run(async () =>
             {
                 try
                 {
                     await foreach (var heartBeatResponse in this._inprocRpc.ReadAllResponseAsync<HeartBeatResponse>(subscribeRequest.NodeName, null, context.CancellationToken))
                     {
                         try
                         {
                             var httpContext = context.GetHttpContext();
                             var remoteIPAddress = httpContext.Connection.RemoteIpAddress;
                             var nodeInfo = this._nodeInfoDbContext == null ? null : await this._nodeInfoDbContext.FindAsync<NodeInfo>(heartBeatResponse.Content.NodeName);
                             if (nodeInfo == null)
                             {
                                 nodeInfo = new NodeInfo()
                                 {
                                     node_name = heartBeatResponse.Content.NodeName,
                                 };
                             }
                             nodeInfo.update_time = heartBeatResponse.Content.Properties["DateTime"];
                             nodeInfo.version = heartBeatResponse.Content.Properties["Version"];
                             nodeInfo.ip_addresses = remoteIPAddress.ToString();
                         }
                         catch (Exception ex)
                         {
                             this.Logger.LogError($"NodeName:{subscribeRequest.NodeName}:{ex}");
                         }

                     }
                 }
                 catch (Exception ex)
                 {
                     this.Logger.LogError($"NodeName:{subscribeRequest.NodeName}:{ex}");
                 }

             }, context.CancellationToken);


            _ = Task.Run((async () =>
            {
                try
                {
                    await foreach (var request in this._inprocRpc.ReadAllRequestAsync<RequestMessage>(subscribeRequest.NodeName, null, context.CancellationToken))
                    {
                        try
                        {
                            SubscribeEvent subscribeEvent = new SubscribeEvent()
                            {
                                EventId = Guid.NewGuid().ToString(),
                                NodeName = subscribeRequest.NodeName,
                            };
                            subscribeEvent.Properties.Add("DateTime", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fff"));
                            subscribeEvent = this._builders[request.GetType()].Invoke(request, subscribeEvent);
                            await responseStream.WriteAsync(subscribeEvent);
                        }
                        catch (Exception ex)
                        {
                            this.Logger.LogError($"NodeName:{subscribeRequest.NodeName}:{ex}");
                        }

                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError($"NodeName:{subscribeRequest.NodeName}:{ex}");
                }


            }), context.CancellationToken);

            while (!context.CancellationToken.IsCancellationRequested)
            {
                string id = Guid.NewGuid().ToString();
                await this._inprocRpc.PostAsync(subscribeRequest.NodeName, new HeartBeatRequest()
                {
                    Key = id,
                    Timeout = TimeSpan.FromSeconds(30),
                    Content = new HeartBeatReq()
                    {
                        RequestId = id,
                    },
                    DateTime = DateTime.Now
                }, context.CancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(15), context.CancellationToken);
            }
        }

        public override Task<Empty> SendFileSystemListDirectoryResponse(FileSystemListDirectoryRsp request, ServerCallContext context)
        {
            this.Logger.LogInformation(request.ToString());
            this._inprocRpc.WriteResponseAsync(request.NodeName, new FileSystemListDirectoryResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult<Empty>(new Empty());
        }

        public override Task<Empty> SendFileSystemListDriveResponse(FileSystemListDriveRsp request, ServerCallContext context)
        {
            this.Logger.LogInformation(request.ToString());
            this._inprocRpc.WriteResponseAsync(request.NodeName, new FileSystemListDriveResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult<Empty>(new Empty());
        }

        public override Task<Empty> SendHeartBeatResponse(HeartBeatRsp request, ServerCallContext context)
        {
            this.Logger.LogInformation(request.ToString());
            this._inprocRpc.WriteResponseAsync(request.NodeName, new HeartBeatResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> SendFileSystemBulkOperationReport(FileSystemBulkOperationReport request, ServerCallContext context)
        {
            this.Logger.LogInformation(request.ToString());
            await this._inprocMessageQueue.PostMessageAsync(request.NodeName, new FileSystemBulkOperationReportMessage()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return new Empty();
        }

        public override Task<Empty> SendFileSystemBulkOperationResponse(FileSystemBulkOperationRsp request, ServerCallContext context)
        {
            this.Logger.LogInformation(request.ToString());
            this._inprocRpc.WriteResponseAsync(request.NodeName, new FileSystemBulkOperationResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult(new Empty());
        }

        public override async Task<QueryConfigurationRsp> QueryConfigurations(QueryConfigurationReq request, ServerCallContext context)
        {
            QueryConfigurationRsp queryConfigurationRsp = new QueryConfigurationRsp();
            queryConfigurationRsp.RequestId = request.RequestId;
            queryConfigurationRsp.NodeName = request.NodeName;

            try
            {
                {
                    string remotePath = string.Format(this._ftpServerConfig.nodeServiceConfigDir, this._serverConfig.Channel, request.NodeName);

                    var nodeConfig = await this._memoryCache.GetOrCreateAsync<NodeConfig>(remotePath, async (cacheEntry) =>
                    {
                        if (!this._memoryCache.TryGetValue<NodeConfig>(remotePath, out var nodeConfig))
                        {
                            await this._asyncFtpClient.AutoConnect();
                            nodeConfig = await this._asyncFtpClient.DownloadAsJson<NodeConfig>(remotePath, this.Logger, context.CancellationToken);
                            if (nodeConfig != null)
                            {
                                cacheEntry.Value = nodeConfig;
                                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                            }
                        }
                        return nodeConfig;
                    });
                    if (nodeConfig!=null)
                    {
                        queryConfigurationRsp.Configurations.Add(ConfigurationKeys.NodeConfig, JsonSerializer.Serialize(nodeConfig));
                    }

                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, ToString());
                queryConfigurationRsp.ErrorCode = ex.HResult;
                queryConfigurationRsp.Message = ex.Message;
            }


            return queryConfigurationRsp;
        }

    }
}
