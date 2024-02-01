using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using JobsWorkerWebService.GrpcServices;
using JobsWorkerWebService.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace JobsWorkerNodeService.Services
{
    public class GrpcService : BackgroundService
    {
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
                SourceStream.Position = position;

            }

            public override void Flush()
            {
                SourceStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return SourceStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return SourceStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                SourceStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                SourceStream.Write(buffer, offset, count);
            }

            public override void Close()
            {
                IsClosed = true;
                SourceStream.Close();
                base.Close();
            }
        }

        private class FileUploadInfo
        {
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
            public required JobsWorker.JobsWorkerClient Client { get; set; }

            public required SubscribeEvent SubscribeEvent { get; set; }

            public required CancellationToken CancellationToken { get; set; }
        }

        private class BulkUploadFileOperation : IDisposable
        {
            public required HttpClient HttpClient { get; set; }

            public required MultipartFormDataContent MultipartFormDataContent { get; set; }

            public required Uri RequestUri { get; set; }

            public required FileSystemOperationState Status { get; set; }

            public required FileSystemBulkOperationReport Report { get; set; }

            public Result<UploadFileResult>? Result { get; set; }

            public required JobsWorker.JobsWorkerClient Client { get; set; }

            public required List<FileUploadInfo> FileUploadList { get; set; }

            public Exception? Exception { get; set; }

            public async Task SendResultReportAsync()
            {
                if (Result != null)
                {
                    if (Result.ErrorCode == 0)
                    {
                        if (Result.Value != null
                        &&
                        Result.Value.UploadedFiles != null)
                        {
                            foreach (var item in FileUploadList)
                            {
                                var uploadedFile = Result.Value.UploadedFiles.FirstOrDefault(x => x.Name == item.Progress.FullName);
                                if (uploadedFile != null)
                                {
                                    item.Progress.Properties.Add("DownloadUrl", uploadedFile.DownloadUrl);
                                }
                            }
                            await Client.SendFileSystemBulkOperationReportAsync(Report);
                            Status = FileSystemOperationState.Finished;
                        }
                    }
                }
            }

            public async Task SendExceptionReportAsync()
            {
                if (this.Exception != null)
                {
                    foreach (var item in this.FileUploadList)
                    {
                        item.SetException(this.Exception);
                    }
                    await this.Client.SendFileSystemBulkOperationReportAsync(this.Report);
                    this.Status = FileSystemOperationState.Failed;
                }
            }

            public void Dispose()
            {
                foreach (var item in this.FileUploadList)
                {
                    item.Stream?.Dispose();
                }
                this.MultipartFormDataContent.Dispose();
                this.HttpClient.Dispose();
            }
        }


        private readonly ILogger<GrpcService> _logger;
        private readonly ActionBlock<SubscribeEventInfo> _eventActionBlock;
        private readonly ActionBlock<BulkUploadFileOperation> _uploadFileActionBlock;
        private readonly Options _options;
        public GrpcService(ILogger<GrpcService> logger, Options options)
        {
            this._options = options;
            this._logger = logger;
            this._eventActionBlock = new ActionBlock<SubscribeEventInfo>(ProcessSubscribeEventAsync, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = 4,
            });

            this._uploadFileActionBlock = new ActionBlock<BulkUploadFileOperation>(ProcessUploadFileAsync,
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = 4,
                });

        }

        private async Task ProcessSubscribeEventAsync(SubscribeEventInfo subscribeEventInfo)
        {
            try
            {
                await ProcessSubscribeEventAsync(subscribeEventInfo.Client, subscribeEventInfo.SubscribeEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

        private async Task ProcessUploadFileAsync(BulkUploadFileOperation bulkOploadFileOperation)
        {
            try
            {
                bulkOploadFileOperation.Status = FileSystemOperationState.Running;
                var rspMsg = await bulkOploadFileOperation.HttpClient.PostAsync(bulkOploadFileOperation.RequestUri, bulkOploadFileOperation.MultipartFormDataContent);
                rspMsg.EnsureSuccessStatusCode();
                var result = await rspMsg.Content.ReadFromJsonAsync<Result<UploadFileResult>>();
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
                _logger.LogError(ex.ToString());
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
                    await ExecuteGrpcClientAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task ExecuteGrpcClientAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                var dnsName = Dns.GetHostName();
                using var channel = GrpcChannel.ForAddress(this._options., new GrpcChannelOptions()
                {
                    HttpHandler = handler
                });
                var client = new JobsWorker.JobsWorkerClient(channel);

                var eventChannel = Channel.CreateUnbounded<SubscribeEvent>();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var subscribeEvent in eventChannel.Reader.ReadAllAsync(cancellationToken))
                        {
                            this._eventActionBlock.Post(new SubscribeEventInfo()
                            {
                                Client = client,
                                SubscribeEvent = subscribeEvent,
                                CancellationToken = cancellationToken,
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex.ToString());
                    }

                }, cancellationToken);

                var subscribeCall = client.Subscribe(new SubscribeRequest()
                {
                    MachineName = dnsName
                });

                while (await subscribeCall.ResponseStream.MoveNext(cancellationToken))
                {
                    var subscribeEvent = subscribeCall.ResponseStream.Current;
                    _logger.LogInformation(subscribeEvent.ToString());
                    await eventChannel.Writer.WriteAsync(subscribeEvent);
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            finally
            {

            }
        }

        private async Task ProcessSubscribeEventAsync(
            JobsWorker.JobsWorkerClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken = default)
        {
            try
            {
                switch (subscribeEvent.ReportCase)
                {
                    case SubscribeEvent.ReportOneofCase.None:
                        break;
                    case SubscribeEvent.ReportOneofCase.HeartBeatReq:
                        await ProcessHeartBeatReq(client, subscribeEvent);
                        break;
                    case SubscribeEvent.ReportOneofCase.FileSystemListDirectoryReq:
                        await ProcessFileSystemListDirectoryReq(client, subscribeEvent);
                        break;
                    case SubscribeEvent.ReportOneofCase.FileSystemListDriveReq:
                        await ProcessFileSystemListDriveReq(client, subscribeEvent);
                        break;
                    case SubscribeEvent.ReportOneofCase.FileSystemBulkOperationReq:
                        await ProcessFileSystemBulkOperationReq(client, subscribeEvent);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task ProcessFileSystemBulkOperationReq(
            JobsWorker.JobsWorkerClient client,
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
                        MachineName = subscribeEvent.MachineName,
                    }, null, null, cancellationToken);
                    await ProcessFileSystemOpenReqAsync(client, subscribeEvent);
                    break;
                default:
                    break;
            }
        }

        private async Task ProcessFileSystemOpenReqAsync(
            JobsWorker.JobsWorkerClient client,
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
                    MachineName = subscribeEvent.FileSystemBulkOperationReq.MachineName,
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

        private void ProcessFileSystemRenameReq(JobsWorker.JobsWorkerClient client, SubscribeEvent subscribeEvent)
        {

        }

        private void ProcessFileSystemDeleteReq(JobsWorker.JobsWorkerClient client, SubscribeEvent subscribeEvent)
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

        private async Task ProcessHeartBeatReq(JobsWorker.JobsWorkerClient client, SubscribeEvent subscribeEvent)
        {
            HeartBeatRsp heartBeatRsp = new HeartBeatRsp();
            heartBeatRsp.MachineName = subscribeEvent.MachineName;
            heartBeatRsp.RequestId = subscribeEvent.HeartBeatReq.RequestId;
            heartBeatRsp.Properties.Add("DateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm::ss"));
            await client.SendHeartBeatResponseAsync(heartBeatRsp);
        }

        private async Task ProcessFileSystemListDriveReq(JobsWorker.JobsWorkerClient client, SubscribeEvent subscribeEvent)
        {
            FileSystemListDriveRsp fileSystemListDriveRsp = new FileSystemListDriveRsp();
            fileSystemListDriveRsp.MachineName = subscribeEvent.MachineName;
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
            await client.SendFileSystemListDriveResponseAsync(fileSystemListDriveRsp);
        }

        private async Task ProcessFileSystemListDirectoryReq(JobsWorker.JobsWorkerClient client, SubscribeEvent subscribeEvent)
        {
            FileSystemListDirectoryRsp fileSystemListDirectoryRsp = new FileSystemListDirectoryRsp();
            fileSystemListDirectoryRsp.MachineName = subscribeEvent.FileSystemListDirectoryReq.MachineName;
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

            await client.SendFileSystemListDirectoryResponseAsync(fileSystemListDirectoryRsp);
        }

    }
}
