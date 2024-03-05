using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.MessageQueues;
using JobsWorker.Shared.MessageQueues.Models;
using JobsWorkerWebService.Services.VirtualSystem;
using System.Collections.Generic;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public partial class NodesController : Controller
    {
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IInprocMessageQueue<string, string, Message> _inprocMessageQueue;
        private readonly IInprocMessageQueue<string, string, NodeConfigTemplateNotificationMessage> _nodeConfigChangedMessage;
        private readonly IInprocRpc<string, string, RequestMessage, ResponseMessage> _inprocRpc;
        private readonly IMemoryCache _memoryCache;
        private readonly IVirtualFileSystem _virtualFileSystem;
        private readonly VirtualFileSystemConfig _virtualFileSystemConfig;
        private readonly ILogger<NodesController> _logger;


        public NodesController(
            IMemoryCache memoryCache,
            IVirtualFileSystem virtualFileSystem,
            VirtualFileSystemConfig virtualFileSystemConfig,
            ApplicationDbContext applicationDbContext,
            ILogger<NodesController> logger,
            IInprocRpc<string, string, RequestMessage, ResponseMessage> inprocRpc,
            IInprocMessageQueue<string, string, Message> inprocMessageQueue)
        {
            this._logger = logger;
            this._applicationDbContext = applicationDbContext;
            this._inprocRpc = inprocRpc;
            this._inprocMessageQueue = inprocMessageQueue;
            this._memoryCache = memoryCache;
            this._virtualFileSystem = virtualFileSystem;
            this._virtualFileSystemConfig = virtualFileSystemConfig;
        }

        [HttpGet("/api/nodes/list")]
        public async Task<ApiResult<IEnumerable<NodeInfoModel>>> QueryNodeListAsync()
        {
            ApiResult<IEnumerable<NodeInfoModel>> apiResult = new ApiResult<IEnumerable<NodeInfoModel>>();
            try
            {
                apiResult.Result =
                    await this._applicationDbContext
                    .NodeInfoDbSet
                    .Include(x => x.Profile)
                    .AsSplitQuery()
                    .ToArrayAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.ToString();
            }
            return apiResult;
        }

        [HttpGet("/api/nodes/{id}")]
        public async Task<ApiResult<NodeInfoModel>> QueryNodeInfoAsync(string id)
        {
            ApiResult<NodeInfoModel> apiResult = new ApiResult<NodeInfoModel>();
            try
            {
                var nodeInfo =
                    await this._applicationDbContext
                    .NodeInfoDbSet
                    .FindAsync(id);
                await this._applicationDbContext.LoadAsync(nodeInfo);
                apiResult.Result = nodeInfo;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.ToString();
            }
            return apiResult;
        }


    }
}
