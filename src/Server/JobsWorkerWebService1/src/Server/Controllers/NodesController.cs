
using JobsWorker.Shared;
using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Models;
using JobsWorkerWebService.Server.Data;
using JobsWorkerWebService.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobsWorkerWebService.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class NodesController : Controller
    {
        private readonly NodeInfoDbContext _nodeInfoDbContext;
        private readonly IInprocRpc<string, string, RequestMessage, ResponseMessage> _inprocessMessageQueue;

        public NodesController(
            NodeInfoDbContext nodeInfoDbContext,
            IInprocRpc<string, string, RequestMessage, ResponseMessage> inprocessMessageQueue)
        {
            this._nodeInfoDbContext = nodeInfoDbContext;
            this._inprocessMessageQueue = inprocessMessageQueue;
        }

        [HttpGet("/api/nodes/list")]
        public Task<IEnumerable<NodeInfo>> QueryNodeListAsync()
        {
            return Task.FromResult<IEnumerable<NodeInfo>>(this._nodeInfoDbContext.NodeInfoDbSet);
        }


        [HttpGet("/api/nodes/{nodeName}/filesystem/{**path}")]
        public async Task<FileSystemListResult> ListNodeDirectory(string nodeName, string path, [FromQuery] string? searchpattern)
        {
            FileSystemListResult fileSystemListResult = new FileSystemListResult();
            try
            {
                string requestId = Guid.NewGuid().ToString();
                FileSystemListDirectoryRequest fileSystemListRequest = new FileSystemListDirectoryRequest()
                {

                    Key = requestId,
                    Content = new FileSystemListDirectoryReq()
                    {
                        NodeName = nodeName,
                        IncludeSubDirectories = false,
                        Directory = path,
                        RequestId = requestId,
                        SearchPattern = searchpattern,
                        Timeout = 60000
                    },
                    Timeout = TimeSpan.FromMicroseconds(60000),
                    DateTime = DateTime.Now,
                };
                FileSystemListDirectoryResponse rsp =
                    await this._inprocessMessageQueue.SendAsync<FileSystemListDirectoryResponse>(nodeName, fileSystemListRequest);
                fileSystemListResult.ErrorCode = rsp.Content.ErrorCode;
                fileSystemListResult.Message = rsp.Content.Message;
                fileSystemListResult.FileSystemObjects = rsp.Content.FileSystemObjects;
            }
            catch (Exception ex)
            {
                fileSystemListResult.ErrorCode = ex.HResult;
                fileSystemListResult.Message = ex.Message;
            }
            return fileSystemListResult;
        }


    }
}
