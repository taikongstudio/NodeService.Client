using JobsWorker.Shared;
using JobsWorker.Shared.Models;

namespace JobsWorkerNodeService.Services
{
    public partial class GrpcService
    {
        private class BulkUploadFileOperation : IDisposable
        {
            public required HttpClient HttpClient { get; set; }

            public required MultipartFormDataContent MultipartFormDataContent { get; set; }

            public required Uri RequestUri { get; set; }

            public required FileSystemOperationState Status { get; set; }

            public required FileSystemBulkOperationReport Report { get; set; }

            public ApiResult<UploadFileResult>? Result { get; set; }

            public required NodeService.NodeServiceClient Client { get; set; }

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
                                var uploadedFile = Result.Value.UploadedFiles.FirstOrDefault(x => x.FileId == item.FileId);
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
    }
}
