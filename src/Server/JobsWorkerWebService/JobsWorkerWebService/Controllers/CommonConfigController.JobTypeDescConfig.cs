using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/jobtypedesc/addorupdate")]
        public async Task<ApiResult<IEnumerable<JobTypeDescConfigModel>>> AddOrUpdateAsync([FromBody] JobTypeDescConfigModel[] JobTypeDescConfigs)
        {
            ApiResult<IEnumerable<JobTypeDescConfigModel>> apiResult = new ApiResult<IEnumerable<JobTypeDescConfigModel>>();
            try
            {
                foreach (var JobTypeDescConfig in JobTypeDescConfigs)
                {
                    await this._applicationDbContext.AddOrUpdateAsync(JobTypeDescConfig);
                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await this.QueryJobTypeDescConfigListAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/jobtypedesc/list")]
        public async Task<ApiResult<IEnumerable<JobTypeDescConfigModel>>> QueryJobTypeDescConfigListAsync()
        {
            ApiResult<IEnumerable<JobTypeDescConfigModel>> apiResult = new ApiResult<IEnumerable<JobTypeDescConfigModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext.JobTypeDescConfigsDbSet
                    .ToArrayAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


        [HttpPost("/api/commonconfig/jobtypedesc/remove")]
        public async Task<ApiResult<IEnumerable<JobTypeDescConfigModel>>> RemoveAsync([FromBody] JobTypeDescConfigModel[] JobTypeDescConfigs)
        {
            ApiResult<IEnumerable<JobTypeDescConfigModel>> apiResult = new ApiResult<IEnumerable<JobTypeDescConfigModel>>();
            try
            {
                foreach (var jobTypeDescConfig in JobTypeDescConfigs)
                {
                    var jobTypeDescConfigFromDb = await this._applicationDbContext.JobTypeDescConfigsDbSet.FindAsync(jobTypeDescConfig.Id);
                    if (jobTypeDescConfig.JobScheduleConfigs.Count != 0)
                    {
                        apiResult.ErrorCode = -1;
                        apiResult.Message = "remove failed";
                        break;
                    }
                    this._applicationDbContext.JobTypeDescConfigsDbSet.RemoveRange(jobTypeDescConfig);
                }
                if (apiResult.ErrorCode == 0)
                {
                    await this._applicationDbContext.SaveChangesAsync();
                    apiResult = await this.QueryJobTypeDescConfigListAsync();
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
