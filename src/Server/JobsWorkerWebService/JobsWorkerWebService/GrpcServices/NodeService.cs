using AntDesign.Charts;
using FluentFTP;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using JobsWorker.Shared;
using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Data;
using JobsWorkerWebService.Extensions;
using JobsWorkerWebService.Models.Configurations;
using JobsWorkerWebService.Services;
using JobsWorkerWebService.Services.VirtualSystem;
using JobsWorkerWebService.Services.VirtualSystemServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace JobsWorkerWebService.GrpcServices
{
    public class NodeService : JobsWorker.Shared.NodeService.NodeServiceBase
    {
        private readonly ILogger<NodeService> Logger;
        private readonly IHeartBeatResponseHandler _heartBeatResponseHandler;
        private readonly IInprocRpc<string, string, RequestMessage, ResponseMessage> _inprocRpc;
        private readonly IInprocMessageQueue<string, string, Message> _inprocMessageQueue;
        private readonly Dictionary<System.Type, Func<RequestMessage, SubscribeEvent, SubscribeEvent>> _builders;
        private readonly IVirtualFileSystem _virtualFileSystem;
        private readonly VirtualFileSystemConfig _virtualFileSystemConfig;
        private readonly IMemoryCache _memoryCache;

        public NodeService(
            IInprocRpc<string, string, RequestMessage, ResponseMessage> inprocRpc,
            IInprocMessageQueue<string, string, Message> inprocMessageQueue,
            IVirtualFileSystem  virtualFileSystem,
            VirtualFileSystemConfig  virtualFileSystemConfig,
            IMemoryCache memoryCache,
            ApplicationDbContext applicationDbContext,
            ILogger<NodeService> logger,
            IHeartBeatResponseHandler heartBeatResponseHandler
            )
        {
            this._inprocRpc = inprocRpc;
            this._virtualFileSystem = virtualFileSystem;
            this._inprocMessageQueue = inprocMessageQueue;
            this._virtualFileSystemConfig = virtualFileSystemConfig;
            this._memoryCache = memoryCache;
            this._builders = new Dictionary<System.Type, Func<RequestMessage, SubscribeEvent, SubscribeEvent>>()
            {
                { typeof(HeartBeatRequest),BuildHeartBeatEvent},
                { typeof(FileSystemListDirectoryRequest),BuildFileSystemListDirectoryRequest},
                { typeof(FileSystemListDriveRequest),BuildFileSystemListDriveRequest},
                { typeof(FileSystemBulkOperationRequest),BuildFileSystemBulkOperationRequest}
            };
            this.Logger = logger;
            this._heartBeatResponseHandler = heartBeatResponseHandler;
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
                     await foreach (var heartBeatResponse in _inprocRpc.ReadAllResponseAsync<HeartBeatResponse>(subscribeRequest.NodeName, null, context.CancellationToken))
                     {
                         try
                         {
                             var httpContext = context.GetHttpContext();
                             await this._heartBeatResponseHandler.HandleAsync(httpContext, heartBeatResponse);
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


            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var request in _inprocRpc.ReadAllRequestAsync<RequestMessage>(subscribeRequest.NodeName, null, context.CancellationToken))
                    {
                        try
                        {
                            SubscribeEvent subscribeEvent = new SubscribeEvent()
                            {
                                EventId = Guid.NewGuid().ToString(),
                                NodeName = subscribeRequest.NodeName,
                            };
                            subscribeEvent.Properties.Add("DateTime", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fff"));
                            subscribeEvent = _builders[request.GetType()].Invoke(request, subscribeEvent);
                            await responseStream.WriteAsync(subscribeEvent);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"NodeName:{subscribeRequest.NodeName}:{ex}");
                        }

                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"NodeName:{subscribeRequest.NodeName}:{ex}");
                }


            }, context.CancellationToken);

            while (!context.CancellationToken.IsCancellationRequested)
            {
                string id = Guid.NewGuid().ToString();
                await _inprocRpc.PostAsync(subscribeRequest.NodeName, new HeartBeatRequest()
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
            Logger.LogInformation(request.ToString());
            _inprocRpc.WriteResponseAsync(request.NodeName, new FileSystemListDirectoryResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> SendFileSystemListDriveResponse(FileSystemListDriveRsp request, ServerCallContext context)
        {
            Logger.LogInformation(request.ToString());
            _inprocRpc.WriteResponseAsync(request.NodeName, new FileSystemListDriveResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> SendHeartBeatResponse(HeartBeatRsp request, ServerCallContext context)
        {
            Logger.LogInformation(request.ToString());
            _inprocRpc.WriteResponseAsync(request.NodeName, new HeartBeatResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> SendFileSystemBulkOperationReport(FileSystemBulkOperationReport request, ServerCallContext context)
        {
            Logger.LogInformation(request.ToString());
            await _inprocMessageQueue.PostMessageAsync(request.NodeName, new FileSystemBulkOperationReportMessage()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return new Empty();
        }

        public override Task<Empty> SendFileSystemBulkOperationResponse(FileSystemBulkOperationRsp request, ServerCallContext context)
        {
            Logger.LogInformation(request.ToString());
            _inprocRpc.WriteResponseAsync(request.NodeName, new FileSystemBulkOperationResponse()
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
                foreach (var configKey in request.ConfigurationKeys)
                {
                    string remotePath = this._virtualFileSystemConfig.GetConfigPath(request.NodeName, configKey);

                    var config = await _memoryCache.GetOrCreateAsync(remotePath, async (cacheEntry) =>
                    {
                        if (!_memoryCache.TryGetValue(remotePath, out var configCache))
                        {
                            using var stream = await this._virtualFileSystem.ReadFileAsync(remotePath, context.CancellationToken);
                            configCache = JsonSerializer.Deserialize<object>(stream);
                            if (configCache != null)
                            {
                                cacheEntry.Value = configCache;
                                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                            }
                        }
                        return configCache;
                    });
                    if (config != null)
                    {
                        queryConfigurationRsp.Configurations.Add(configKey, JsonSerializer.Serialize(config));
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ToString());
                queryConfigurationRsp.ErrorCode = ex.HResult;
                queryConfigurationRsp.Message = ex.Message;
            }


            return queryConfigurationRsp;
        }

    }
}
