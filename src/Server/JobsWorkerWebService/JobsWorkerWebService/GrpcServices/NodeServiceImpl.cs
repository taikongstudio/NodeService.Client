

using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.MessageQueues;
using JobsWorker.Shared.MessageQueues.Models;
using System.Globalization;

namespace JobsWorkerWebService.GrpcServices
{
    public class NodeServiceImpl : NodeService.NodeServiceBase
    {
        private readonly ILogger<NodeServiceImpl> Logger;
        private readonly IHeartBeatResponseHandler _heartBeatResponseHandler;
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IInprocRpc<string, string, RequestMessage, ResponseMessage> _inprocRpc;
        private readonly IInprocMessageQueue<string, string, Message> _inprocMessageQueue;
        private readonly Dictionary<System.Type, Func<RequestMessage, SubscribeEvent, SubscribeEvent>> _builders;
        private readonly IVirtualFileSystem _virtualFileSystem;
        private readonly VirtualFileSystemConfig _virtualFileSystemConfig;
        private readonly IMemoryCache _memoryCache;
        private readonly ConcurrentDictionary<(string NodeName, string InstanceId), JobExecutionLog> _JobExecutionLogDict;
        private readonly ActionBlock<JobExecutionLog> _writeLogActionBlock;

        public NodeServiceImpl(
            ApplicationDbContext applicationDbContext,
            IInprocRpc<string, string, RequestMessage, ResponseMessage> inprocRpc,
            IInprocMessageQueue<string, string, Message> inprocMessageQueue,
            IVirtualFileSystem virtualFileSystem,
            VirtualFileSystemConfig virtualFileSystemConfig,
            IMemoryCache memoryCache,
            ILogger<NodeServiceImpl> logger,
            IHeartBeatResponseHandler heartBeatResponseHandler
            )
        {
            this._applicationDbContext = applicationDbContext;
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
                { typeof(FileSystemBulkOperationRequest),BuildFileSystemBulkOperationRequest},
                { typeof(NodeConfigTemplateChangedMessage),BuildNodeConfigurationChangedRequest},
                { typeof(JobExecutionTriggerRequest),BuildTaskTriggerRequest}
            };
            this.Logger = logger;
            this._heartBeatResponseHandler = heartBeatResponseHandler;
            this._JobExecutionLogDict = new ConcurrentDictionary<(string NodeName, string InstanceId), JobExecutionLog>();
            this._writeLogActionBlock = new ActionBlock<JobExecutionLog>(OnProcessWriteLogActionBlock);
        }

        private async Task OnProcessWriteLogActionBlock(JobExecutionLog JobExecutionLog)
        {
            try
            {
                var key = (JobExecutionLog.NodeName, JobExecutionLog.InstanceId);
                using var memoryStream = new MemoryStream();
                using var streamWriter = new StreamWriter(memoryStream);
                foreach (var log in JobExecutionLog.Logs)
                {
                    streamWriter.WriteLine(log);
                }
                JobExecutionLog.Logs.Clear();
                memoryStream.Position = 0;
                await this._virtualFileSystem.UploadStream(JobExecutionLog.LogPath, memoryStream);
                this._JobExecutionLogDict.TryRemove(key, out _);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

        private SubscribeEvent BuildNodeConfigurationChangedRequest(RequestMessage message, SubscribeEvent subscribeEvent)
        {
            subscribeEvent.Topic = "nodeconfig";
            subscribeEvent.ConfigurationChangedReport = (message as NodeConfigTemplateChangedMessage).Content;
            return subscribeEvent;
        }

        private SubscribeEvent BuildTaskTriggerRequest(RequestMessage message, SubscribeEvent subscribeEvent)
        {
            subscribeEvent.Topic = "task";
            subscribeEvent.JobExecutionTriggerReq = (message as JobExecutionTriggerRequest).Content;
            return subscribeEvent;
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


            }, context.CancellationToken);

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
                        NodeName = subscribeRequest.NodeName,
                    },
                    DateTime = DateTime.Now
                }, context.CancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);
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
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> SendFileSystemListDriveResponse(FileSystemListDriveRsp request, ServerCallContext context)
        {
            Logger.LogInformation(request.ToString());
            this._inprocRpc.WriteResponseAsync(request.NodeName, new FileSystemListDriveResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> SendHeartBeatResponse(HeartBeatRsp request, ServerCallContext context)
        {
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
                var configName = request.Parameters["ConfigName"];
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(queryConfigurationRsp.NodeName);
                await this._applicationDbContext.LoadAsync(nodeInfo);
                if (nodeInfo == null)
                {
                    queryConfigurationRsp.ErrorCode = -1;
                    queryConfigurationRsp.Message = $"Could not found node info:{queryConfigurationRsp.NodeName}";
                }
                else if (nodeInfo.ActiveNodeConfigTemplateForeignKey == null)
                {
                    queryConfigurationRsp.ErrorCode = -1;
                    queryConfigurationRsp.Message = $"{queryConfigurationRsp.NodeName}`s config is not configured yet";
                }
                else
                {
                    queryConfigurationRsp.Configurations.Add(configName, nodeInfo.ActiveNodeConfigTemplate?.ToJsonString<NodeConfigTemplateModel>());
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


        public override async Task<Empty> SendJobExecutionReport(IAsyncStreamReader<JobExecutionReport> requestStream, ServerCallContext context)
        {

            while (await requestStream.MoveNext())
            {
                var JobExecutionReport = requestStream.Current;
                var nodeName = JobExecutionReport.NodeName;
                var id = JobExecutionReport.Properties[nameof(JobExecutionInstanceModel.Id)];
                var JobExecutionStatus = JobExecutionReport.Status;
                var key = (nodeName, id);
                JobExecutionInstanceModel? JobExecutionRecord =
                    await this._applicationDbContext
                    .JobExecutionInstancesDbSet
                    .FindAsync(nodeName, id);
                if (JobExecutionRecord.LogPath == null)
                {
                    JobExecutionRecord.LogPath = this._virtualFileSystemConfig.GetTaskLogsPath(nodeName, id);
                }
                JobExecutionLog? JobExecutionLog = EnsureJobExecutionLog(key);
                foreach (var log in JobExecutionReport.Logs)
                {
                    JobExecutionLog.Logs.Add(log);
                }
                switch (JobExecutionStatus)
                {
                    case JobExecutionStatus.Unknown:
                        break;
                    case JobExecutionStatus.Triggered:
                        break;
                    case JobExecutionStatus.Started:
                        var begin_time = JobExecutionReport.Properties[nameof(JobExecutionInstanceModel.ExecutionBeginTime)];
                        JobExecutionRecord.ExecutionBeginTime =
                            DateTime.ParseExact(begin_time, 
                            NodePropertyModel.DateTimeFormatString,
                            DateTimeFormatInfo.InvariantInfo);
                        break;
                    case JobExecutionStatus.Running:
                        break;
                    case JobExecutionStatus.Failed:
                    case JobExecutionStatus.Finished:
                        var end_time = JobExecutionReport.Properties[nameof(JobExecutionInstanceModel.ExecutionEndTime)];
                        JobExecutionRecord.ExecutionEndTime = DateTime.ParseExact(end_time,
                            NodePropertyModel.DateTimeFormatString,
                            DateTimeFormatInfo.InvariantInfo);
                        this._writeLogActionBlock.Post(JobExecutionLog);
                        break;
                    default:
                        break;
                }

                JobExecutionRecord.Status = (JobExecutionInstanceStatus)JobExecutionStatus;
                await this._applicationDbContext.SaveChangesAsync();
            }
            return new Empty();
        }

        private JobExecutionLog EnsureJobExecutionLog((string nodeName, string instance_id) key)
        {
            if (!this._JobExecutionLogDict.TryGetValue(key, out var JobExecutionLog))
            {
                JobExecutionLog = new JobExecutionLog()
                {
                    NodeName = key.nodeName,
                    Logs = new List<string>()
                };
                this._JobExecutionLogDict.TryAdd(key, JobExecutionLog);
            }

            return JobExecutionLog;
        }

        public override async Task<Empty> SendJobExecutionTriggerResponse(JobExecutionTriggerRsp request, ServerCallContext context)
        {
            this.Logger.LogInformation(request.ToString());
            await this._inprocRpc.WriteResponseAsync(request.NodeName, new JobExecutionTriggerResponse()
            {
                Key = request.RequestId,
                Content = request,
                DateTime = DateTime.Now
            });
            return new Empty();
        }
    }
}
