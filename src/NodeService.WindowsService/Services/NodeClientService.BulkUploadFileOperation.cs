
namespace NodeService.WindowsService.Services
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
    }
}
