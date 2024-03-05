using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/restapi/addorupdate")]
        public async Task<ApiResult<IEnumerable<RestApiConfigModel>>> AddOrUpdateAsync([FromBody] RestApiConfigModel[] restApiConfigs)
        {
            ApiResult<IEnumerable<RestApiConfigModel>> apiResult = new ApiResult<IEnumerable<RestApiConfigModel>>();
            try
            {
                foreach (var restApiConfig in restApiConfigs)
                {
                    var restApiConfigFromDb = await this._applicationDbContext.RestApiConfigsDbSet.FindAsync(restApiConfig.Id);
                    if (restApiConfigFromDb == null)
                    {
                        await this._applicationDbContext
                            .RestApiConfigsDbSet
                            .AddAsync(restApiConfig);
                    }
                    else
                    {
                        restApiConfigFromDb.With(restApiConfig);
                    }
                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await this.QueryRestApiConfigListAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/restapi/list")]
        public async Task<ApiResult<IEnumerable<RestApiConfigModel>>> QueryRestApiConfigListAsync()
        {
            ApiResult<IEnumerable<RestApiConfigModel>> apiResult = new ApiResult<IEnumerable<RestApiConfigModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext.RestApiConfigsDbSet
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


        [HttpPost("/api/commonconfig/restapi/remove")]
        public async Task<ApiResult<IEnumerable<RestApiConfigModel>>> RemoveAsync([FromBody] RestApiConfigModel[] restApiConfigs)
        {
            ApiResult<IEnumerable<RestApiConfigModel>> apiResult = new ApiResult<IEnumerable<RestApiConfigModel>>();
            try
            {
                this._applicationDbContext.RestApiConfigsDbSet.RemoveRange(restApiConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await this.QueryRestApiConfigListAsync();
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
