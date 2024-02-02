
using JobsWorker.Shared;
using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Data;
using JobsWorkerWebService.Extensions;
using JobsWorkerWebService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class NodesController : Controller
    {
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IInprocRpc<string, string, RequestMessage, ResponseMessage> _inprocessMessageQueue;
        private readonly IMemoryCache _memoryCache;

        public NodesController(
            IMemoryCache memoryCache,
            ApplicationDbContext nodeInfoDbContext,
            IInprocRpc<string, string, RequestMessage, ResponseMessage> inprocessMessageQueue)
        {
            this._applicationDbContext = nodeInfoDbContext;
            this._inprocessMessageQueue = inprocessMessageQueue;
            this._memoryCache = memoryCache;
        }

        [HttpGet("/api/nodes/list")]
        public Task<IEnumerable<NodeInfo>> QueryNodeListAsync()
        {
            return Task.FromResult<IEnumerable<NodeInfo>>(_applicationDbContext.NodeInfoDbSet);
        }

        [HttpGet("/api/nodes/{nodeName}/props")]
        public async Task<ApiResult<IEnumerable<NodePropertyItem>>> QueryNodeListAsync(string nodeName)
        {
            ApiResult<IEnumerable<NodePropertyItem>> apiResult = new ApiResult<IEnumerable<NodePropertyItem>>();
            var nodeDict = await this._memoryCache.GetOrCreateNodePropsAsync(nodeName);
            apiResult.Value = nodeDict.Select(x => new NodePropertyItem()
            {
                Name = x.Key,
                Value = x.Value,
            });
            return apiResult;
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
