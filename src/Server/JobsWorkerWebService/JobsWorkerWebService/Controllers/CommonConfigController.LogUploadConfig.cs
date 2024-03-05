using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/logupload/addorupdate")]
        public async Task<ApiResult<IEnumerable<LogUploadConfigModel>>> AddOrUploadAsync([FromBody] LogUploadConfigModel[] logUploadConfigs)
        {
            ApiResult<IEnumerable<LogUploadConfigModel>> apiResult = new ApiResult<IEnumerable<LogUploadConfigModel>>();
            try
            {
                foreach (var logUploadConfig in logUploadConfigs)
                {
                    var logUploadConfigFromDb = await this._applicationDbContext.LogUploadConfigsDbSet.FindAsync(logUploadConfig.Id);
                    if (logUploadConfigFromDb == null)
                    {
                        await this._applicationDbContext
                            .LogUploadConfigsDbSet
                            .AddAsync(logUploadConfig);
                    }
                    else
                    {
                        logUploadConfigFromDb.With(logUploadConfig);
                    }

                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await this.QueryLogUploadConfigsAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/logupload/list")]
        public async Task<ApiResult<IEnumerable<LogUploadConfigModel>>> QueryLogUploadConfigsAsync()
        {
            ApiResult<IEnumerable<LogUploadConfigModel>> apiResult = new ApiResult<IEnumerable<LogUploadConfigModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext.LogUploadConfigsDbSet
                    .Include(x => x.TemplateBindingList)
                    .ToArrayAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


        [HttpPost("/api/commonconfig/logupload/remove")]
        public async Task<ApiResult<IEnumerable<LogUploadConfigModel>>> RemoveAsync([FromBody] LogUploadConfigModel[] logUploadConfigs)
        {
            ApiResult<IEnumerable<LogUploadConfigModel>> apiResult = new ApiResult<IEnumerable<LogUploadConfigModel>>();
            try
            {
                this._applicationDbContext.LogUploadConfigsDbSet.RemoveRange(logUploadConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.LogUploadConfigsDbSet;
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
