
using Google.Protobuf.WellKnownTypes;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {
        private class BulkUploadFileOperation : IDisposable
        {
            public required HttpClient HttpClient { get; set; }

            public required MultipartFormDataContent MultipartFormDataContent { get; set; }

            public required Uri RequestUri { get; set; }

            public required FileSystemOperationState Status { get; set; }

            public required FileSystemBulkOperationReport Report { get; set; }

            public ApiResponse<UploadFileResult>? Result { get; set; }

            public required NodeServiceClient Client { get; set; }

            public required List<FileUploadInfo> FileUploadList { get; set; }

            public Exception? Exception { get; set; }

            public async Task SendResultReportAsync()
            {
                if (Result != null)
                {
                    if (Result.ErrorCode == 0)
                    {
                        if (Result.Result != null
                        &&
                        Result.Result.UploadedFiles != null)
                        {
                            foreach (var item in FileUploadList)
                            {
                                var uploadedFile = Result.Result.UploadedFiles.FirstOrDefault(x => x.FileId == item.FileId);
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
                if (Exception != null)
                {
                    foreach (var item in FileUploadList)
                    {
                        item.SetException(Exception);
                    }
                    await Client.SendFileSystemBulkOperationReportAsync(Report);
                    Status = FileSystemOperationState.Failed;
                }
            }

            public void Dispose()
            {
                foreach (var item in FileUploadList)
                {
                    item.Stream?.Dispose();
                }
                MultipartFormDataContent.Dispose();
                HttpClient.Dispose();
            }
        }

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
