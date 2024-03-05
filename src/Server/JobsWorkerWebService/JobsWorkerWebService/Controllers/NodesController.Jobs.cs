using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class NodesController
    {

        [HttpGet("/api/nodes/{id}/jobs/list")]
        public async Task<ApiResult<IEnumerable<JobScheduleConfigModel>>> GetNodeTaskListAsync(string id)
        {
            ApiResult<IEnumerable<JobScheduleConfigModel>> apiResult = new ApiResult<IEnumerable<JobScheduleConfigModel>>();
            try
            {

                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(id);
                if (nodeInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = $"invalid node id:{id}";
                }
                else
                {
                    await this._applicationDbContext.LoadAsync(nodeInfo);
                    apiResult.Result = nodeInfo.JobScheduleConfigs ?? [];
                }

            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


        [HttpGet("/api/nodes/{id}/jobs/{jobId}/instances/list")]
        public async Task<ApiResult<IEnumerable<JobExecutionInstanceModel>>> GetNodeJobInstancesAsync(string id, string jobId)
        {
            ApiResult<IEnumerable<JobExecutionInstanceModel>> apiResult = new ApiResult<IEnumerable<JobExecutionInstanceModel>>();
            try
            {

                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(id);
                if (nodeInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = $"invalid node id:{id}";
                }
                else
                {
                    await this._applicationDbContext.LoadAsync(nodeInfo);
                    apiResult.Result = nodeInfo.JobExecutionInstances ?? [];
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
