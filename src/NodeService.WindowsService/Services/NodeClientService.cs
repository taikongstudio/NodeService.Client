using NodeService.Infrastructure.Interfaces;
using NodeService.WindowsService.Collections;

namespace NodeService.WindowsService.Services
{
    public partial class NodeClientService : Microsoft.Extensions.Hosting.BackgroundService
    {
        private class StreamPosition
        {
            public int Position { get; set; }
            public int Length { get; set; }
        }

        private class FileUploadInfo
        {
            public required string FileId { get; set; }

            public required FileSystemOperationProgress Progress { get; set; }

            public ObservableStream? Stream { get; set; }

            public Exception? Exception { get; private set; }

            public void SetException(Exception exception)
            {
                Exception = exception;
                Progress.ErrorCode = exception.HResult;
                Progress.Message = exception.Message;
                Progress.State = FileSystemOperationState.Failed;
                Progress.IsCompleted = true;
            }
        }

        private class SubscribeEventInfo
        {
            public required NodeServiceClient Client { get; set; }

            public required SubscribeEvent SubscribeEvent { get; set; }

            public required CancellationToken CancellationToken { get; set; }
        }



        private readonly ActionBlock<SubscribeEventInfo> _subscribeEventActionBlock;
        private readonly ActionBlock<BulkUploadFileOperation> _uploadFileActionBlock;
        private readonly NodeIdentityProvider _nodeIdProvider;
        private readonly Metadata _headers;
        private readonly IConfiguration _configuration;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly string? _activeGrpcAddress;
        private NodeServiceClient _nodeServiceClient;
        private IScheduler _scheduler;

        public ILogger<NodeClientService> _logger { get; private set; }


        public NodeClientService(
            IServiceProvider serviceProvider,
            ILogger<NodeClientService> logger,
            IConfiguration configuration,
            ISchedulerFactory schedulerFactory,
            IAsyncQueue<JobExecutionContext> jobExecutionContextQueue,
            IAsyncQueue<JobExecutionReport> reportQueue,
            JobContextDictionary jobContextDictionary,
            NodeIdentityProvider machineIdProvider
            )
        {
            _jobContextDictionary = jobContextDictionary;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _schedulerFactory = schedulerFactory;
            _jobExecutionContextQueue = jobExecutionContextQueue;
            _reportQueue = reportQueue;
            _jobContextDictionary = jobContextDictionary;
            _logger = logger;
            _subscribeEventActionBlock = new ActionBlock<SubscribeEventInfo>(ProcessSubscribeEventAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                EnsureOrdered = true,
            });

            _uploadFileActionBlock = new ActionBlock<BulkUploadFileOperation>(ProcessUploadFileAsync,
            new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : 8,
            });
            _nodeIdProvider = machineIdProvider;
            _headers = new Metadata();
            _heartBeatCounter = Random.Shared.Next(0, 10);
        }

        private async Task ProcessSubscribeEventAsync(SubscribeEventInfo subscribeEventInfo)
        {
            try
            {
                await ProcessSubscribeEventAsync(subscribeEventInfo.Client,
                                                 subscribeEventInfo.SubscribeEvent,
                                                 subscribeEventInfo.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private async Task ProcessUploadFileAsync(BulkUploadFileOperation op)
        {
            try
            {
                op.Status = FileSystemOperationState.Running;
                var rspMsg = await op.HttpClient.PostAsync(op.RequestUri, op.MultipartFormDataContent);
                rspMsg.EnsureSuccessStatusCode();
                var result = await rspMsg.Content.ReadFromJsonAsync<ApiResponse<UploadFileResult>>();
                if (result.ErrorCode != 0)
                {
                    throw new Exception(result.Message)
                    {
                        HResult = result.ErrorCode,
                    };
                }
                op.Result = result;
                await op.SendResultReportAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                op.Exception = ex;
                await op.SendExceptionReportAsync();
            }
            finally
            {
                op.Dispose();
            }
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {

            try
            {
                _scheduler = await _schedulerFactory.GetScheduler();
                if (!_scheduler.IsStarted)
                {
                    await _scheduler.Start(stoppingToken);
                }

                _headers.AppendNodeClientHeaders(new NodeClientHeaders()
                {
                    HostName = Dns.GetHostName(),
                    NodeId = _nodeIdProvider.GetNodeId()
                });

                while (!stoppingToken.IsCancellationRequested)
                {
                    await RunGrpcLoopAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task RunGrpcLoopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                var dnsName = Dns.GetHostName();
                string? address = GetAddressFromConfiguration();
                _logger.LogInformation($"Grpc Address:{address}");

                using var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions()
                {
                    HttpHandler = handler,
                    Credentials = ChannelCredentials.SecureSsl,
                });

                _nodeServiceClient = new NodeServiceClient(channel);
                using var subscribeCall = _nodeServiceClient.Subscribe(new SubscribeRequest()
                {
                    RequestId = Guid.NewGuid().ToString(),
                }, _headers, cancellationToken: cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {

                        using var reportStreamingCall = _nodeServiceClient.SendJobExecutionReport(_headers, cancellationToken: cancellationToken);
                        Stopwatch stopwatch = new Stopwatch();
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            int messageCount = 0;
                            stopwatch.Start();
                            while (_reportQueue.TryPeek(out var reportMessage))
                            {
                                await reportStreamingCall.RequestStream.WriteAsync(reportMessage, cancellationToken);
                                await _reportQueue.DeuqueAsync(cancellationToken);
                                messageCount++;
                            }
                            stopwatch.Stop();
                            if (messageCount > 0)
                            {
                                _logger.LogInformation($"Sent {messageCount} messages,spent:{stopwatch.Elapsed}");
                            }
                            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }


                }, cancellationToken: cancellationToken);

                await foreach (var subscribeEvent in subscribeCall.ResponseStream.ReadAllAsync(cancellationToken))
                {
                    _logger.LogInformation(subscribeEvent.ToString());
                    _subscribeEventActionBlock.Post(new SubscribeEventInfo()
                    {
                        Client = _nodeServiceClient,
                        SubscribeEvent = subscribeEvent,
                        CancellationToken = cancellationToken,
                    });
                }

            }
            catch (RpcException ex)
            {
                _logger.LogError(ex.ToString());
                if (ex.StatusCode == StatusCode.Cancelled)
                {

                }
                await Task.Delay(TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(Debugger.IsAttached ? 5 : 30), cancellationToken);
            }
            finally
            {

            }

        }

        private string? GetAddressFromConfiguration()
        {
            try
            {
                return _activeGrpcAddress ?? _configuration.GetValue<string>("ServerConfig:GrpcAddress");
            }
            catch (Exception ex)
            {
                return null;
            }

        }

        private async Task<bool> QueryNodeConfigTemplateAsync(string dnsName,
            NodeServiceClient nodeServiceClient, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var queryConfigurationReq = new QueryConfigurationRequest()
                {
                    RequestId = Guid.NewGuid().ToString()
                };


                queryConfigurationReq.Parameters.Add("ConfigName", "NodeConfig");
                var queryConfigurationRsp = await nodeServiceClient.QueryConfigurationsAsync(queryConfigurationReq, _headers);
                var nodeConfigString = queryConfigurationRsp.Configurations[ConfigurationKeys.NodeConfigTemplate];

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        //private async Task PostNodeConfigChangedEventAsync(string requestId, string nodeConfigString, CancellationToken cancellationToken)
        //{
        //    NodeConfigTemplateModel nodeConfig = JsonSerializer.Deserialize<NodeConfigTemplateModel>(nodeConfigString);
        //    NodeConfigChangedEvent nodeConfigUpdateEvent = new NodeConfigChangedEvent()
        //    {
        //        Key = requestId,
        //        Content = nodeConfig,
        //        DateTime = DateTime.Now
        //    };
        //    await _inprocMessageQueue.PostAync(nameof(NodeConfigService), nodeConfigUpdateEvent, cancellationToken);
        //}

        private async Task ProcessSubscribeEventAsync(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            switch (subscribeEvent.EventCase)
            {
                case SubscribeEvent.EventOneofCase.None:
                    break;
                case SubscribeEvent.EventOneofCase.HeartBeatRequest:
                    await ProcessHeartBeatRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemListDirectoryRequest:
                    await ProcessFileSystemListDirectoryRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemListDriveRequest:
                    await ProcessFileSystemListDriveRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.FileSystemBulkOperationRequest:
                    await ProcessFileSystemBulkOperationRequest(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.ConfigurationChangedReport:
                    //await ProcessConfigurationChangedReport(client, subscribeEvent, cancellationToken);
                    break;
                case SubscribeEvent.EventOneofCase.JobExecutionEventRequest:
                    await ProcessJobExecutionEventRequest(client, subscribeEvent, cancellationToken);
                    break;
                default:
                    break;
            }
        }

        private async Task ProcessJobExecutionEventRequest(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken)
        {
            var req = subscribeEvent.JobExecutionEventRequest;
            try
            {
                await ProcessJobExecutionRequestEventAsync(req);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }



        //private async Task ProcessConfigurationChangedReport(
        //    NodeService.NodeServiceClient client,
        //    SubscribeEvent subscribeEvent,
        //    CancellationToken cancellationToken = default)
        //{
        //    var nodeConfigString = subscribeEvent.ConfigurationChangedReport.Configurations[ConfigurationKeys.NodeConfig];
        //    await PostNodeConfigChangedEventAsync(subscribeEvent.ConfigurationChangedReport.RequestId, nodeConfigString, cancellationToken);
        //}

        private async Task ProcessFileSystemBulkOperationRequest(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            switch (subscribeEvent.FileSystemBulkOperationRequest.Operation)
            {
                case FileSystemOperation.None:
                    break;
                case FileSystemOperation.Create:
                    break;
                case FileSystemOperation.Delete:
                    //ProcessFileSystemDeleteReq(client, subscribeEvent);
                    break;
                case FileSystemOperation.Move:
                    break;
                case FileSystemOperation.Rename:
                    //ProcessFileSystemRenameReq(client, subscribeEvent);
                    break;
                case FileSystemOperation.Open:
                    await client.SendFileSystemBulkOperationResponseAsync(new FileSystemBulkOperationResponse()
                    {
                        ErrorCode = 0,
                        Message = string.Empty,
                        RequestId = subscribeEvent.FileSystemBulkOperationRequest.RequestId,
                    }, _headers, null, cancellationToken);
                    await ProcessFileSystemOpenRequestAsync(client, subscribeEvent);
                    break;
                default:
                    break;
            }
        }

        private async Task ProcessFileSystemOpenRequestAsync(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            var requestUri = subscribeEvent.FileSystemBulkOperationRequest.Headers["RequestUri"];

            var bulkUploadFileOperation = new BulkUploadFileOperation()
            {
                HttpClient = new HttpClient(),
                MultipartFormDataContent = new MultipartFormDataContent(),
                RequestUri = new Uri(requestUri),
                Status = FileSystemOperationState.NotStarted,
                Report = new FileSystemBulkOperationReport
                {
                    RequestId = subscribeEvent.FileSystemBulkOperationRequest.RequestId,
                    OriginalRequestId = subscribeEvent.FileSystemBulkOperationRequest.RequestId,
                    State = FileSystemOperationState.NotStarted
                },
                Client = client,
                FileUploadList = []
            };
            foreach (var path in subscribeEvent.FileSystemBulkOperationRequest.PathList)
            {
                FileUploadInfo fileUploadInfo = new()
                {
                    FileId = Guid.NewGuid().ToString(),
                    Progress = new FileSystemOperationProgress()
                    {
                        FullName = path,
                        Progress = 0,
                        Message = string.Empty,
                        ErrorCode = 0,
                        Operation = subscribeEvent.FileSystemBulkOperationRequest.Operation,
                        State = FileSystemOperationState.NotStarted,
                    }
                };
                try
                {
                    ObservableStream observableStream = new(File.OpenRead(path));
                    var fileContent = new StreamContent(observableStream);
                    fileContent.Headers.Add("FileId", fileUploadInfo.FileId);
                    bulkUploadFileOperation.MultipartFormDataContent.Add(fileContent, "files", path);
                    fileUploadInfo.Stream = observableStream;
                }
                catch (Exception ex)
                {
                    fileUploadInfo.SetException(ex);
                }


                bulkUploadFileOperation.FileUploadList.Add(fileUploadInfo);
                bulkUploadFileOperation.Report.Progresses.Add(fileUploadInfo.Progress);
            }


            if (cancellationToken.IsCancellationRequested)
            {
                bulkUploadFileOperation.Dispose();
                return;
            }

            _uploadFileActionBlock.Post(bulkUploadFileOperation);

            try
            {
                int completedCount = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100);
                    if (completedCount == bulkUploadFileOperation.FileUploadList.Count)
                    {
                        break;
                    }

                    foreach (var fileUploadInfo in bulkUploadFileOperation.FileUploadList)
                    {
                        if (fileUploadInfo.Progress.IsCompleted)
                        {
                            completedCount++;
                            continue;
                        }
                        if (fileUploadInfo.Exception == null)
                        {
                            if (bulkUploadFileOperation.Status == FileSystemOperationState.Finished)
                            {
                                continue;
                            }
                            if (fileUploadInfo.Stream == null)
                            {
                                continue;
                            }
                            if (fileUploadInfo.Stream.IsClosed)
                            {
                                continue;
                            }
                            fileUploadInfo.Progress.State =
                                fileUploadInfo.Stream.Position == fileUploadInfo.Stream.Length ? FileSystemOperationState.Finished : FileSystemOperationState.Running;
                            fileUploadInfo.Progress.Progress = fileUploadInfo.Stream.Position / (fileUploadInfo.Stream.Length + 0d);
                            if (fileUploadInfo.Progress.State == FileSystemOperationState.Finished)
                            {
                                fileUploadInfo.Progress.IsCompleted = true;
                                fileUploadInfo.Progress.Message = "完成";
                            }
                        }
                        else
                        {
                            fileUploadInfo.Progress.Progress = 0;
                            fileUploadInfo.Progress.State = FileSystemOperationState.Failed;
                            fileUploadInfo.Progress.ErrorCode = fileUploadInfo.Exception.HResult;
                            fileUploadInfo.Progress.Message = fileUploadInfo.Exception.Message;
                            fileUploadInfo.Progress.IsCompleted = true;
                        }
                    }
                    _logger.LogInformation(bulkUploadFileOperation.Report.ToString());
                    await client.SendFileSystemBulkOperationReportAsync(bulkUploadFileOperation.Report, null, null, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }




        }

        private void ProcessFileSystemRenameReq(NodeServiceClient client, SubscribeEvent subscribeEvent)
        {

        }

        private void ProcessFileSystemDeleteRequest(NodeServiceClient client, SubscribeEvent subscribeEvent)
        {
            foreach (var path in subscribeEvent.FileSystemBulkOperationRequest.PathList)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }



        private async Task ProcessFileSystemListDriveRequest(NodeServiceClient client, SubscribeEvent subscribeEvent, CancellationToken cancellationToken = default)
        {
            FileSystemListDriveResponse fileSystemListDriveRsp = new FileSystemListDriveResponse();
            fileSystemListDriveRsp.RequestId = subscribeEvent.FileSystemListDriveRequest.RequestId;
            fileSystemListDriveRsp.Drives.AddRange(DriveInfo.GetDrives().Select(x => new FileSystemDriveInfo()
            {
                Name = x.Name,
                TotalFreeSpace = x.TotalFreeSpace,
                AvailableFreeSpace = x.AvailableFreeSpace,
                TotalSize = x.TotalSize,
                DriveFormat = x.DriveFormat,
                DriveType = x.DriveType.ToString(),
                IsReady = x.IsReady,
                RootDirectory = x.RootDirectory.FullName,
                VolumeLabel = x.VolumeLabel
            }));
            await client.SendFileSystemListDriveResponseAsync(fileSystemListDriveRsp, null, null, cancellationToken);
        }

        private async Task ProcessFileSystemListDirectoryRequest(NodeServiceClient client, SubscribeEvent subscribeEvent, CancellationToken cancellationToken = default)
        {
            FileSystemListDirectoryResponse fileSystemListDirectoryRsp = new FileSystemListDirectoryResponse();
            fileSystemListDirectoryRsp.RequestId = subscribeEvent.FileSystemListDirectoryRequest.RequestId;
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(subscribeEvent.FileSystemListDirectoryRequest.Directory);
                foreach (var info in directoryInfo.EnumerateFileSystemInfos())
                {
                    fileSystemListDirectoryRsp.FileSystemObjects.Add(new FileSystemObject()
                    {
                        Name = info.Name,
                        FullName = info.FullName,
                        CreationTime = info.CreationTime.ToUniversalTime().ToTimestamp(),
                        LastWriteTime = info.LastWriteTime.ToUniversalTime().ToTimestamp(),
                        Length = info is FileInfo ? (info as FileInfo).Length : 0,
                        Type = info.Attributes.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                fileSystemListDirectoryRsp.ErrorCode = ex.HResult;
                fileSystemListDirectoryRsp.Message = ex.Message;
            }

            await client.SendFileSystemListDirectoryResponseAsync(fileSystemListDirectoryRsp, null, null, cancellationToken);
        }

    }
}
