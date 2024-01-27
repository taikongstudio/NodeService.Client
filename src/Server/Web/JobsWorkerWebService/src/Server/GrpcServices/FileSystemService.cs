using Grpc.Core;
using JobsWorkerWebService.GrpcServices;
using JobsWorkerWebService.Server.FileSystemServices;
using JobsWorkerWebService.Server.GrpcServices;
using JobsWorkerWebService.Server.Services;

namespace JobsWorkerWebService.Server.GrpcServices
{
    public class FileSystemService :FileSystem.FileSystemBase
    {
        private readonly ILogger<FileSystemService> _logger;
        private readonly IInprocRpc<string, FileSystemRequest, FileSystemResponse> _inprocRpc;
        public FileSystemService(ILogger<FileSystemService> logger,
            IInprocRpc<string, FileSystemRequest, FileSystemResponse> inprocRpc)
        {
            _logger = logger;
            _inprocRpc = inprocRpc;
        }

        public override async Task<FileSystemListRsp> List(FileSystemListReq request, ServerCallContext context)
        {
            FileSystemListRsp fileSystemListRsp = new FileSystemListRsp();
            try
            {
                this._logger.LogInformation($"{request}");
                var rsp = await this._inprocRpc.SendRequestAsync<FileSystemListResponse>(request.MachineName, new FileSystemListRequest()
                {
                    Id = Guid.Parse(request.RequestId),
                    IncludeSubDirectories = request.IncludeSubDirectories,
                    Path = request.Path,
                    SearchPattern = request.SearchPattern,
                    Timeout = request.Timeout,
                }, context.CancellationToken);
                fileSystemListRsp.RequestId = rsp.Id.ToString();
                fileSystemListRsp.ErrorCode = rsp.ErrorCode;
                fileSystemListRsp.ErrorMessage = rsp.ErrorMessage;
                fileSystemListRsp.Items.AddRange(rsp.FileSystemObjects);
             
            }
            catch (Exception ex)
            {
                fileSystemListRsp.ErrorCode = ex.HResult;
                fileSystemListRsp.ErrorMessage = ex.Message;
            }
            return fileSystemListRsp;
        }


    }
}
