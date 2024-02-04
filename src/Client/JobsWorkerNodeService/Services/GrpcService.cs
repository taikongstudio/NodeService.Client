﻿using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using JobsWorker.Shared;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.Models;
using JobsWorkerNodeService.Helper;
using JobsWorkerNodeService.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace JobsWorkerNodeService.Services
{
    public partial class GrpcService : BackgroundService
    {
        private class StreamPosition
        {
            public int Position { get; set; }
            public int Length { get; set; }
        }

        private class ObservableStream : Stream
        {

            public Stream SourceStream { get; private set; }

            public ObservableStream(Stream stream)
            {
                this.SourceStream = stream;
            }

            public bool IsClosed { get; private set; }

            public override bool CanRead => SourceStream.CanRead;

            public override bool CanSeek => SourceStream.CanSeek;

            public override bool CanWrite => SourceStream.CanWrite;

            public override long Length => SourceStream.Length;

            public override long Position { get => SourceStream.Position; set => SetPosition(value); }

            private void SetPosition(long position)
            {
                this.SourceStream.Position = position;

            }

            public override void Flush()
            {
                this.SourceStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return this.SourceStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return this.SourceStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                this.SourceStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.SourceStream.Write(buffer, offset, count);
            }

            public override void Close()
            {
                this.IsClosed = true;
                this.SourceStream.Close();
                base.Close();
            }
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
            public required NodeService.NodeServiceClient Client { get; set; }

            public required SubscribeEvent SubscribeEvent { get; set; }

            public required CancellationToken CancellationToken { get; set; }
        }



        private readonly ActionBlock<SubscribeEventInfo> _subscribeEventActionBlock;
        private readonly ActionBlock<BulkUploadFileOperation> _uploadFileActionBlock;
        private readonly IConfiguration _configuration;
        private readonly IInprocMessageQueue<string, string, NodeConfigChangedEvent> _inprocMessageQueue;

        public ILogger<GrpcService> Logger { get; private set; }


        public GrpcService(
            IInprocMessageQueue<string, string, NodeConfigChangedEvent> inprocMessageQueue, 
            ILogger<GrpcService> logger, 
            IConfiguration  configuration)
        {
            this._inprocMessageQueue = inprocMessageQueue;
            this._configuration = configuration;
            this.Logger = logger;
            this._subscribeEventActionBlock = new ActionBlock<SubscribeEventInfo>(ProcessSubscribeEventAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : 8,
                EnsureOrdered = true,
            });

            this._uploadFileActionBlock = new ActionBlock<BulkUploadFileOperation>(ProcessUploadFileAsync,
            new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : 8,
            });
        }

        private async Task ProcessSubscribeEventAsync(SubscribeEventInfo subscribeEventInfo)
        {
            try
            {
                await this.ProcessSubscribeEventAsync(subscribeEventInfo.Client, subscribeEventInfo.SubscribeEvent);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

        private async Task ProcessUploadFileAsync(BulkUploadFileOperation bulkOploadFileOperation)
        {
            try
            {
                bulkOploadFileOperation.Status = FileSystemOperationState.Running;
                var rspMsg = await bulkOploadFileOperation.HttpClient.PostAsync(bulkOploadFileOperation.RequestUri, bulkOploadFileOperation.MultipartFormDataContent);
                rspMsg.EnsureSuccessStatusCode();
                var result = await rspMsg.Content.ReadFromJsonAsync<ApiResult<UploadFileResult>>();
                if (result.ErrorCode != 0)
                {
                    throw new Exception(result.Message)
                    {
                        HResult = result.ErrorCode,
                    };
                }
                bulkOploadFileOperation.Result = result;
                await bulkOploadFileOperation.SendResultReportAsync();
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
                bulkOploadFileOperation.Exception = ex;
                await bulkOploadFileOperation.SendExceptionReportAsync();
            }
            finally
            {
                bulkOploadFileOperation.Dispose();
            }
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await RunGrpcLoopAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
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
                this.Logger.LogInformation($"Grpc Address:{address}");
                using var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions()
                {
                    HttpHandler = handler,
                    Credentials = ChannelCredentials.SecureSsl
                });
                var jobWorkerClient = new NodeService.NodeServiceClient(channel);
                _ = Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (await this.QueryNodeConfigAsync(dnsName, jobWorkerClient, cancellationToken))
                        {
                            break;
                        }
                        this.Logger.LogError("Failed to query config,sleep 30s");
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    }
                }, cancellationToken);

                var subscribeCall = jobWorkerClient.Subscribe(new SubscribeRequest()
                {
                    NodeName = dnsName
                });

                while (await subscribeCall.ResponseStream.MoveNext(cancellationToken))
                {
                    var subscribeEvent = subscribeCall.ResponseStream.Current;
                    this.Logger.LogInformation(subscribeEvent.ToString());
                    this._subscribeEventActionBlock.Post(new SubscribeEventInfo()
                    {
                        Client = jobWorkerClient,
                        SubscribeEvent = subscribeEvent,
                        CancellationToken = cancellationToken,
                    });
                }


            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            finally
            {

            }
        }

        private string? GetAddressFromConfiguration()
        {
            try
            {
                return this._configuration.GetValue<string>("GrpcConfig:Address");
            }
            catch (Exception ex)
            {
                return null;
            }

        }

        private async Task<bool> QueryNodeConfigAsync(string dnsName, NodeService.NodeServiceClient nodeServiceClient, CancellationToken cancellationToken = default)
        {
            try
            {
                var queryConfigurationReq = new QueryConfigurationReq()
                {
                    RequestId = Guid.NewGuid().ToString(),
                    NodeName = dnsName,
                };
                queryConfigurationReq.ConfigurationKeys.Add(ConfigurationKeys.NodeConfig);
                var queryConfigurationRsp = await nodeServiceClient.QueryConfigurationsAsync(queryConfigurationReq);
                var nodeConfigString = queryConfigurationRsp.Configurations[ConfigurationKeys.NodeConfig];
                await this.PostNodeConfigChangedEventAsync(queryConfigurationRsp.RequestId, nodeConfigString, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
            return false;
        }

        private async Task PostNodeConfigChangedEventAsync(string requestId, string nodeConfigString, CancellationToken cancellationToken)
        {
            NodeConfig nodeConfig = JsonSerializer.Deserialize<NodeConfig>(nodeConfigString);
            NodeConfigChangedEvent nodeConfigUpdateEvent = new NodeConfigChangedEvent()
            {
                Key = requestId,
                Content = nodeConfig,
                DateTime = DateTime.Now
            };
            await this._inprocMessageQueue.PostMessageAsync(nameof(NodeConfigService), nodeConfigUpdateEvent, cancellationToken);
        }

        private async Task ProcessSubscribeEventAsync(
            NodeService.NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            try
            {
                switch (subscribeEvent.EventCase)
                {
                    case SubscribeEvent.EventOneofCase.None:
                        break;
                    case SubscribeEvent.EventOneofCase.HeartBeatReq:
                        await this.ProcessHeartBeatReq(client, subscribeEvent, cancellationToken);
                        break;
                    case SubscribeEvent.EventOneofCase.FileSystemListDirectoryReq:
                        await this.ProcessFileSystemListDirectoryReq(client, subscribeEvent, cancellationToken);
                        break;
                    case SubscribeEvent.EventOneofCase.FileSystemListDriveReq:
                        await this.ProcessFileSystemListDriveReq(client, subscribeEvent, cancellationToken);
                        break;
                    case SubscribeEvent.EventOneofCase.FileSystemBulkOperationReq:
                        await this.ProcessFileSystemBulkOperationReq(client, subscribeEvent, cancellationToken);
                        break;
                    case SubscribeEvent.EventOneofCase.ConfigurationChangedReport:
                        await this.ProcessConfigurationChangedReport(client, subscribeEvent, cancellationToken);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        private async Task ProcessConfigurationChangedReport(
            NodeService.NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            var nodeConfigString = subscribeEvent.ConfigurationChangedReport.Configurations[ConfigurationKeys.NodeConfig];
            await this.PostNodeConfigChangedEventAsync(subscribeEvent.ConfigurationChangedReport.RequestId, nodeConfigString, cancellationToken);
        }

        private async Task ProcessFileSystemBulkOperationReq(
            NodeService.NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            switch (subscribeEvent.FileSystemBulkOperationReq.Operation)
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
                    await client.SendFileSystemBulkOperationResponseAsync(new FileSystemBulkOperationRsp()
                    {
                        ErrorCode = 0,
                        Message = string.Empty,
                        RequestId = subscribeEvent.FileSystemBulkOperationReq.RequestId,
                        NodeName = subscribeEvent.NodeName,
                    }, null, null, cancellationToken);
                    await ProcessFileSystemOpenReqAsync(client, subscribeEvent);
                    break;
                default:
                    break;
            }
        }

        private async Task ProcessFileSystemOpenReqAsync(
            NodeService.NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            var requestUri = subscribeEvent.FileSystemBulkOperationReq.Headers["RequestUri"];

            var bulkUploadFileOperation = new BulkUploadFileOperation()
            {
                HttpClient = new HttpClient(),
                MultipartFormDataContent = new MultipartFormDataContent(),
                RequestUri = new Uri(requestUri),
                Status = FileSystemOperationState.NotStarted,
                Report = new FileSystemBulkOperationReport
                {
                    NodeName = subscribeEvent.FileSystemBulkOperationReq.NodeName,
                    RequestId = subscribeEvent.FileSystemBulkOperationReq.RequestId,
                    OriginalRequestId = subscribeEvent.FileSystemBulkOperationReq.RequestId,
                    State = FileSystemOperationState.NotStarted
                },
                Client = client,
                FileUploadList = new List<FileUploadInfo>()
            };
            foreach (var path in subscribeEvent.FileSystemBulkOperationReq.PathList)
            {
                FileUploadInfo fileUploadInfo = new FileUploadInfo()
                {
                    FileId = Guid.NewGuid().ToString(),
                    Progress = new FileSystemOperationProgress()
                    {
                        FullName = path,
                        Progress = 0,
                        Message = string.Empty,
                        ErrorCode = 0,
                        Operation = subscribeEvent.FileSystemBulkOperationReq.Operation,
                        State = FileSystemOperationState.NotStarted,
                    }
                };
                try
                {
                    ObservableStream observableStream = new ObservableStream(File.OpenRead(path));
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
                    Logger.LogInformation(bulkUploadFileOperation.Report.ToString());
                    await client.SendFileSystemBulkOperationReportAsync(bulkUploadFileOperation.Report, null, null, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }




        }

        private void ProcessFileSystemRenameReq(NodeService.NodeServiceClient client, SubscribeEvent subscribeEvent)
        {

        }

        private void ProcessFileSystemDeleteReq(NodeService.NodeServiceClient client, SubscribeEvent subscribeEvent)
        {
            foreach (var path in subscribeEvent.FileSystemBulkOperationReq.PathList)
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

        private async Task ProcessHeartBeatReq(NodeService.NodeServiceClient client, SubscribeEvent subscribeEvent, CancellationToken cancellationToken = default)
        {
            HeartBeatRsp heartBeatRsp = new HeartBeatRsp();
            heartBeatRsp.NodeName = subscribeEvent.NodeName;
            heartBeatRsp.RequestId = subscribeEvent.HeartBeatReq.RequestId;

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                heartBeatRsp.Properties.Add(NodeProperties.LastUpdateDateTime_Key, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff"));
                heartBeatRsp.Properties.Add(NodeProperties.Version_Key, Constants.Version);
                heartBeatRsp.Properties.Add(NodeProperties.Environment_UserName_Key, Environment.UserName);
                heartBeatRsp.Properties.Add(NodeProperties.Environment_ProcessorCount_Key, Environment.ProcessorCount.ToString());
                heartBeatRsp.Properties.Add(NodeProperties.Environment_IsPrivilegedProcess_Key, Environment.IsPrivilegedProcess.ToString());
                heartBeatRsp.Properties.Add(NodeProperties.Environment_UserInteractive_Key, Environment.UserInteractive.ToString());
                heartBeatRsp.Properties.Add(NodeProperties.Environment_SystemDirectory_Key, Environment.SystemDirectory);
                heartBeatRsp.Properties.Add(NodeProperties.Environment_OSVersion_Key, Environment.OSVersion.ToString());
                heartBeatRsp.Properties.Add(NodeProperties.Environment_UserDomainName_Key, Environment.UserDomainName);
                heartBeatRsp.Properties.Add(NodeProperties.Environment_CommandLine_Key, Environment.CommandLine);
                heartBeatRsp.Properties.Add(NodeProperties.Environment_WorkingSet_Key, Environment.WorkingSet.ToString());
                heartBeatRsp.Properties.Add(NodeProperties.Environment_SystemPageSize_Key, Environment.SystemPageSize.ToString());
                heartBeatRsp.Properties.Add(NodeProperties.Environment_ProcessPath_Key, Environment.ProcessPath);
                heartBeatRsp.Properties.Add(NodeProperties.Environment_Version_Key, Environment.Version.ToString());
                heartBeatRsp.Properties.Add(NodeProperties.Environment_LogicalDrives_Key, string.Join(",", Environment.GetLogicalDrives()));
                heartBeatRsp.Properties.Add(NodeProperties.NetworkInterface_IsNetworkAvailable_Key, NetworkInterface.GetIsNetworkAvailable().ToString());

                CollectDomain(heartBeatRsp);

                CollectEnvironmentVariables(heartBeatRsp);

                CollectNetworkInterfaces(heartBeatRsp);

                CollectProcessList(heartBeatRsp);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }


            stopwatch.Stop();

            heartBeatRsp.Properties.Add("CollectTimeSpan", stopwatch.Elapsed.ToString());

            await client.SendHeartBeatResponseAsync(heartBeatRsp, null, null, cancellationToken);

            void CollectNetworkInterfaces(HeartBeatRsp heartBeatRsp)
            {
                try
                {
                    var networkInterfaceModels = NetworkInterface.GetAllNetworkInterfaces().Select(NetworkInterfaceModel.From);
                    heartBeatRsp.Properties.Add(NodeProperties.NetworkInterface_AllNetworkInterfaces_Key, JsonSerializer.Serialize(networkInterfaceModels));
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.ToString());
                }
            }

            void CollectProcessList(HeartBeatRsp heartBeatRsp)
            {
                var processList = CommonHelper.CollectProcessList(this.Logger);

                heartBeatRsp.Properties.Add(NodeProperties.Process_Processes_Key, JsonSerializer.Serialize(processList));
            }

            void CollectEnvironmentVariables(HeartBeatRsp heartBeatRsp)
            {

                List<EnvironmentVariableInfo> environmentVariables = new List<EnvironmentVariableInfo>();
                try
                {
                    foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                    {

                        environmentVariables.Add(new EnvironmentVariableInfo()
                        {
                            Name = entry.Key as string,
                            Value = entry.Value as string
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.ToString());
                }

                heartBeatRsp.Properties.Add(
                    NodeProperties.Environment_EnvironmentVariables_Key,
                    JsonSerializer.Serialize(environmentVariables));
            }

            void CollectDomain(HeartBeatRsp heartBeatRsp)
            {
                try
                {
                    Domain domain = Domain.GetComputerDomain();
                    heartBeatRsp.Properties.Add(NodeProperties.Domain_ComputerDomain_Key, domain.Name);
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.ToString());
                }

            }
        }

        private async Task ProcessFileSystemListDriveReq(NodeService.NodeServiceClient client, SubscribeEvent subscribeEvent, CancellationToken cancellationToken = default)
        {
            FileSystemListDriveRsp fileSystemListDriveRsp = new FileSystemListDriveRsp();
            fileSystemListDriveRsp.NodeName = subscribeEvent.NodeName;
            fileSystemListDriveRsp.RequestId = subscribeEvent.FileSystemListDriveReq.RequestId;
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

        private async Task ProcessFileSystemListDirectoryReq(NodeService.NodeServiceClient client, SubscribeEvent subscribeEvent, CancellationToken cancellationToken = default)
        {
            FileSystemListDirectoryRsp fileSystemListDirectoryRsp = new FileSystemListDirectoryRsp();
            fileSystemListDirectoryRsp.NodeName = subscribeEvent.FileSystemListDirectoryReq.NodeName;
            fileSystemListDirectoryRsp.RequestId = subscribeEvent.FileSystemListDirectoryReq.RequestId;
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(subscribeEvent.FileSystemListDirectoryReq.Directory);
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
