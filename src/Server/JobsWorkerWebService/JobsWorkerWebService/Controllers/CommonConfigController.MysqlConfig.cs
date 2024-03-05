using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/mysql/addorupdate")]
        public async Task<ApiResult<IEnumerable<MysqlConfigModel>>>
            AddOrUpdateAsync([FromBody] MysqlConfigModel[] mysqlConfigs)
        {
            ApiResult<IEnumerable<MysqlConfigModel>> apiResult = new ApiResult<IEnumerable<MysqlConfigModel>>();
            try
            {
                foreach (var mysqlConfig in mysqlConfigs)
                {
                    var mysqlConfigFromDb = await this._applicationDbContext.MysqlConfigsDbSet.FindAsync(mysqlConfig.Id);
                    if (mysqlConfigFromDb == null)
                    {
                        await this._applicationDbContext
                            .MysqlConfigsDbSet
                            .AddAsync(mysqlConfig);
                    }
                    else
                    {
                        mysqlConfigFromDb.With(mysqlConfig);
                    }
                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.MysqlConfigsDbSet;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/mysql/list")]
        public Task<ApiResult<IEnumerable<MysqlConfigModel>>> QueryMysqlConfigAsync()
        {
            ApiResult<IEnumerable<MysqlConfigModel>> apiResult = new ApiResult<IEnumerable<MysqlConfigModel>>();
            try
            {
                apiResult.Result = this._applicationDbContext.MysqlConfigsDbSet;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return Task.FromResult(apiResult);
        }

        [HttpPost("/api/commonconfig/mysql/remove")]
        public async Task<ApiResult<IEnumerable<MysqlConfigModel>>> RemoveAsync([FromBody] MysqlConfigModel[] mysqlConfigs)
        {
            ApiResult<IEnumerable<MysqlConfigModel>> apiResult = new ApiResult<IEnumerable<MysqlConfigModel>>();
            try
            {
                this._applicationDbContext.MysqlConfigsDbSet.RemoveRange(mysqlConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.MysqlConfigsDbSet;
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
