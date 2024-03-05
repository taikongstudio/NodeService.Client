using JobsWorker.Shared.Models;
using JobsWorkerWebService.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace JobsWorkerWebService.Controllers
{
    public partial class NodesController 
    {

        [HttpGet("/api/nodes/{id}/props/list")]
        public async Task<ApiResult<IEnumerable<NodePropertyEntry>>> QueryNodePropsAsync(string id)
        {
            ApiResult<IEnumerable<NodePropertyEntry>> apiResult = new ApiResult<IEnumerable<NodePropertyEntry>>();
            try
            {
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(id);
                if (nodeInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = "invalid node id";
                }
                else
                {
                    await this._applicationDbContext.LoadAsync(nodeInfo);
                    var currentNodePropSnapshot = nodeInfo.LastNodePropertySnapshot;
                    apiResult.Result = currentNodePropSnapshot?.NodeProperties;
                }
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
