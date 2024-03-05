using JobsWorker.Shared.Models;
using JobsWorkerWebService.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace JobsWorkerWebService.Controllers
{
    public partial class NodesController 
    {

        [HttpPost("/api/nodes/{id}/profile/update")]
        public async Task<ApiResult<bool>> UpdateNodeInfoAsync(string id, [FromBody] UpdateNodeProfileModel value)
        {
            ApiResult<bool> apiResult = new ApiResult<bool>();
            try
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(id);
                if (nodeInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = "invalid node id";
                    apiResult.Result = false;
                }
                else
                {
                    await this._applicationDbContext.LoadAsync(nodeInfo);
                    var profile = await this._applicationDbContext.NodeProfilesDbSet.FindAsync(nodeInfo.Profile.Id);
                    if (profile == null)
                    {
                        apiResult.ErrorCode = -1;
                        apiResult.Message = "invalid profile id";
                        apiResult.Result = false;
                    }
                    else
                    {
                        profile.TestInfo = value.TestInfo;
                        profile.LabArea = value.LabArea;
                        profile.LabName = value.LabName;
                        profile.Usages = value.Usages;
                        profile.Remarks = value.Remarks;
                        int changes = await this._applicationDbContext.SaveChangesAsync();
                        apiResult.Result = true;
                    }

                }
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


    }
}
