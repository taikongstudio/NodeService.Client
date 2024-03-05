using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/localdirectorymapping/addorupdate")]
        public async Task<ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>> AddOrUpdateAsync([FromBody] LocalDirectoryMappingConfigModel[] localDirectoryMappingConfigs)
        {
            ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>> apiResult = new ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>();
            try
            {
                foreach (var localDirectoryMappingConfig in localDirectoryMappingConfigs)
                {
                    var localDirectoryConfigFromDb =
                                await this._applicationDbContext
                                .LocalDirectoryMappingConfigsDbSet
                                .FindAsync(localDirectoryMappingConfig.Id);
                    if (localDirectoryMappingConfig.IsDefault)
                    {
                        foreach (var item in this._applicationDbContext.LocalDirectoryMappingConfigsDbSet)
                        {
                            item.IsDefault = false;
                        }
                    }

                    if (localDirectoryConfigFromDb == null)
                    {
                        await this._applicationDbContext
                            .LocalDirectoryMappingConfigsDbSet
                            .AddAsync(localDirectoryMappingConfig);
                    }
                    else
                    {
                        localDirectoryConfigFromDb.With(localDirectoryMappingConfig);
                    }
                }

                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await QueryLocalDirectoryConfigAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/localdirectorymapping/list")]
        public async Task<ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>> QueryLocalDirectoryConfigAsync()
        {
            ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>> apiResult = new ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext.LocalDirectoryMappingConfigsDbSet.ToArrayAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpPost("/api/commonconfig/localdirectorymapping/remove")]
        public async Task<ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>> RemoveAsync([FromBody] LocalDirectoryMappingConfigModel[] localDirectoryMappingConfigs)
        {
            ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>> apiResult = new ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>();
            try
            {
                this._applicationDbContext
                    .LocalDirectoryMappingConfigsDbSet
                    .RemoveRange(localDirectoryMappingConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.LocalDirectoryMappingConfigsDbSet;
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
