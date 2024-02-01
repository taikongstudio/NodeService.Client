using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using JobsWorker.Shared;
using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using Microsoft.Extensions.Logging;

namespace JobsWorkerWebService.Server.GrpcServices
{
    public class FileSystemService : FileSystem.FileSystemBase
    {
        private readonly ILogger<FileSystemService> _logger;
        private readonly IInprocRpc<string, string, RequestMessage, ResponseMessage> _inprocRpc;
        private readonly IInprocMessageQueue<string, string, Message> _inprocMessageQueue;
        public FileSystemService(ILogger<FileSystemService> logger,
            IInprocRpc<string, string, RequestMessage, ResponseMessage> inprocRpc,
            IInprocMessageQueue<string, string, Message> inprocMessageQueue)
        {
            this._logger = logger;
            this._inprocRpc = inprocRpc;
            this._inprocMessageQueue = inprocMessageQueue;
        }

        public override async Task<FileSystemListDriveRsp> ListDrive(FileSystemListDriveReq request, ServerCallContext context)
        {
            FileSystemListDriveRsp fileSystemListDriveRsp = new FileSystemListDriveRsp();
            try
            {
                fileSystemListDriveRsp.RequestId = request.RequestId;
                fileSystemListDriveRsp.NodeName = request.NodeName;
                this._logger.LogInformation($"{request}");
                var rsp = await this._inprocRpc.SendAsync<FileSystemListDriveResponse>(request.NodeName,
                        new FileSystemListDriveRequest()
                        {
                            Key = request.RequestId,
                            Content = request,
                            Timeout = TimeSpan.FromMilliseconds(request.Timeout),
                            DateTime = DateTime.Now,
                        },
                        context.CancellationToken
                    );
                if (rsp == null)
                {
                    throw new TimeoutException();
                }
                fileSystemListDriveRsp.ErrorCode = rsp.Content.ErrorCode;
                fileSystemListDriveRsp.Message = rsp.Content.Message;
                fileSystemListDriveRsp.Drives.AddRange(rsp.Content.Drives);
            }
            catch (Exception ex)
            {
                fileSystemListDriveRsp.ErrorCode = ex.HResult;
                fileSystemListDriveRsp.Message = ex.Message;
                this._logger.LogError($"NodeName:{request.NodeName}:{ex}");
            }
            this._logger.LogInformation(fileSystemListDriveRsp.ToString());
            return fileSystemListDriveRsp;
        }

        public async override Task<FileSystemListDirectoryRsp> ListDirectory(FileSystemListDirectoryReq request, ServerCallContext context)
        {
            FileSystemListDirectoryRsp fileSystemListDirectoryRsp = new FileSystemListDirectoryRsp();
            try
            {
                this._logger.LogInformation($"{request}");
                fileSystemListDirectoryRsp.RequestId = request.RequestId;
                fileSystemListDirectoryRsp.NodeName = request.NodeName;
                var rsp = await this._inprocRpc.SendAsync<FileSystemListDirectoryResponse>(request.NodeName, new FileSystemListDirectoryRequest()
                {
                    Key = request.RequestId,
                    Content = request,
                    Timeout = TimeSpan.FromMilliseconds(request.Timeout),
                    DateTime = DateTime.Now,
                }, context.CancellationToken);
                if (rsp == null)
                {
                    throw new TimeoutException();
                }
                fileSystemListDirectoryRsp.ErrorCode = rsp.Content.ErrorCode;
                fileSystemListDirectoryRsp.Message = rsp.Content.Message;
                fileSystemListDirectoryRsp.FileSystemObjects.AddRange(rsp.Content.FileSystemObjects);
            }
            catch (Exception ex)
            {
                fileSystemListDirectoryRsp.ErrorCode = ex.HResult;
                fileSystemListDirectoryRsp.Message = ex.Message;
                this._logger.LogError($"NodeName:{request.NodeName}:{ex}");
            }
            this._logger.LogInformation(fileSystemListDirectoryRsp.ToString());
            return fileSystemListDirectoryRsp;
        }

        public override async Task<FileSystemBulkOperationRsp> BulkOperaion(FileSystemBulkOperationReq request, ServerCallContext context)
        {
            FileSystemBulkOperationRsp fileSystemBulkOperationRsp = new FileSystemBulkOperationRsp();
            try
            {
                this._logger.LogInformation($"{request}");
                fileSystemBulkOperationRsp.RequestId = request.RequestId;
                fileSystemBulkOperationRsp.NodeName = request.NodeName;
                if (request.Operation == FileSystemOperation.Open)
                {
                    var httpContext = context.GetHttpContext();
                    request.Headers.TryAdd("RequestUri", $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/files/upload/{request.NodeName}");
                }
                var rsp = await this._inprocRpc.SendAsync<FileSystemBulkOperationResponse>(request.NodeName, new FileSystemBulkOperationRequest()
                {

                    Key = request.RequestId,
                    Content = request,
                    DateTime = DateTime.Now,
                    Timeout = TimeSpan.Zero
                }, context.CancellationToken);
                if (rsp == null)
                {
                    throw new TimeoutException();
                }
                fileSystemBulkOperationRsp.ErrorCode = rsp.Content.ErrorCode;
                fileSystemBulkOperationRsp.Message = rsp.Content.Message;
            }
            catch (Exception ex)
            {
                fileSystemBulkOperationRsp.ErrorCode = ex.HResult;
                fileSystemBulkOperationRsp.Message = ex.Message;
                this._logger.LogError($"NodeName:{request.NodeName}:{ex}");
            }
            this._logger.LogInformation(fileSystemBulkOperationRsp.ToString());
            return fileSystemBulkOperationRsp;
        }

        public override Task<FileSystemBulkOperationCancelRsp> CancelBulkOperation(FileSystemBulkOperationCancelReq request, ServerCallContext context)
        {
            return base.CancelBulkOperation(request, context);
        }

        public override async Task<FileSystemQueryBulkOperationReportRsp> QueryBulkOperationReport(FileSystemQueryBulkOperationReportReq request, ServerCallContext context)
        {
            FileSystemQueryBulkOperationReportRsp rsp = new FileSystemQueryBulkOperationReportRsp();
            try
            {
                this._logger.LogInformation(request.ToString());
                rsp.NodeName = request.NodeName;
                rsp.RequestId = request.RequestId;
                rsp.OriginalRequestId = request.OriginalRequestId;
                using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                if (request.Timeout > 0)
                {
                    cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(request.Timeout));
                }
                try
                {
                    await foreach (var reportMessage in this._inprocMessageQueue.ReadAllMessageAsync<FileSystemBulkOperationReportMessage>(
                            request.NodeName,
                            (report) => report.Content.RequestId == request.OriginalRequestId,
                            cancellationTokenSource.Token))
                    {
                        rsp.Reports.Add(reportMessage.Content);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (rsp.Reports.Count == 0)
                    {
                        rsp.ErrorCode = ex.HResult;
                        rsp.Message = ex.Message;
                    }
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex.ToString());
                    rsp.ErrorCode = ex.HResult;
                    rsp.Message = ex.Message;
                }
            }
            catch (Exception ex)
            {
                rsp.ErrorCode = ex.HResult;
                rsp.Message = ex.Message;
                this._logger.LogError($"NodeName:{request.NodeName}:{ex}");
            }

            return rsp;
        }




    }
}
