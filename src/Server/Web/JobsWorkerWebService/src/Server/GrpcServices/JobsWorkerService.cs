using Grpc.Core;
using JobsWorkerWebService.GrpcServices;
using JobsWorkerWebService.Server.FileSystemServices;
using JobsWorkerWebService.Server.GrpcServices;
using JobsWorkerWebService.Server.Services;

namespace JobsWorkerWebService.Server.GrpcServices
{
    public class JobsWorkerService : JobsWorker.JobsWorkerBase
    {
        private readonly ILogger<JobsWorkerService> _logger;
        private readonly IInprocRpc<string, FileSystemRequest, FileSystemResponse> _inprocRpc;
        public JobsWorkerService(ILogger<JobsWorkerService> logger,IInprocRpc<string,FileSystemRequest,FileSystemResponse> inprocRpc)
        {
            _logger = logger;
            _inprocRpc = inprocRpc;
        }

        public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<SubscribeEvent> responseStream, ServerCallContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                FileSystemRequest? fileSystemRequest = null;
                if (this._inprocRpc.TryPeekRequest(request.MachineName, out fileSystemRequest))
                {
                    this._inprocRpc.TryReadRequest(request.MachineName, out fileSystemRequest);
                    SubscribeEvent subscribeEvent = new SubscribeEvent();
                    if (fileSystemRequest is FileSystemListRequest fileSystemListRequest)
                    {
                        subscribeEvent.FileSystemListReq = new FileSystemListReq()
                        {
                            RequestId = fileSystemListRequest.Id.ToString(),
                            IncludeSubDirectories = fileSystemListRequest.IncludeSubDirectories,
                            Path = fileSystemListRequest.Path,
                            SearchPattern = fileSystemListRequest.SearchPattern,
                            Timeout = fileSystemListRequest.Timeout,
                        };
                    }
                    await responseStream.WriteAsync(subscribeEvent);
                }
                await Task.Delay(1000);
            }
        }
    }
}
