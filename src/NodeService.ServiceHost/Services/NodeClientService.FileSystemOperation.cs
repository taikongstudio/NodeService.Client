
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

            public required NodeServiceClient Client { get; set; }

            public required List<FileUploadInfo> FileUploadList { get; set; }

            public Exception? Exception { get; set; }

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                op.Exception = ex;
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
                        RequestId = subscribeEvent.FileSystemBulkOperationRequest.RequestId,
                        ErrorCode = 0,
                        Message = string.Empty,
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
            var ftpConfigId = subscribeEvent.FileSystemBulkOperationRequest.Headers["FtpConfigId"];

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
                bulkUploadFileOperation.FileUploadList.Add(fileUploadInfo);
                bulkUploadFileOperation.Report.Progresses.Add(fileUploadInfo.Progress);
                if (!File.Exists(path))
                {
                    fileUploadInfo.Progress.State = FileSystemOperationState.Failed;
                    fileUploadInfo.Progress.Message = "File not found";
                    continue;
                }

                try
                {
                    ObservableStream observableStream = new(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    var fileContent = new StreamContent(observableStream);
                    fileContent.Headers.Add("FileId", fileUploadInfo.FileId);
                    fileContent.Headers.Add("SourcePath", path);
                    fileContent.Headers.Add("FtpConfigId", ftpConfigId);
                    bulkUploadFileOperation.MultipartFormDataContent.Add(fileContent, "Files", path);
                    fileUploadInfo.Stream = observableStream;
                }
                catch (Exception ex)
                {
                    fileUploadInfo.SetException(ex);
                }



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
                    await Task.Delay(1000, cancellationToken);
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
                            fileUploadInfo.Progress.State =
                                fileUploadInfo.Stream.Position == fileUploadInfo.Stream.Length ? FileSystemOperationState.Finished : FileSystemOperationState.Running;
                            fileUploadInfo.Progress.Progress = fileUploadInfo.Stream.Position / (fileUploadInfo.Stream.Length + 0d);
                            if (fileUploadInfo.Progress.State == FileSystemOperationState.Finished)
                            {
                                fileUploadInfo.Progress.Message = "Completed";
                            }
                        }
                        else
                        {
                            fileUploadInfo.Progress.Progress = 0;
                            fileUploadInfo.Progress.State = FileSystemOperationState.Failed;
                            fileUploadInfo.Progress.ErrorCode = fileUploadInfo.Exception.HResult;
                            fileUploadInfo.Progress.Message = fileUploadInfo.Exception.Message;
                        }
                    }
                    _logger.LogInformation(bulkUploadFileOperation.Report.ToString());
                    await client.SendFileSystemBulkOperationReportAsync(
                        bulkUploadFileOperation.Report,
                        _headers,
                        null,
                        cancellationToken);
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
            await client.SendFileSystemListDriveResponseAsync(fileSystemListDriveRsp, _headers, null, cancellationToken);
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

            await client.SendFileSystemListDirectoryResponseAsync(fileSystemListDirectoryRsp, _headers, null, cancellationToken);
        }

    }
}
