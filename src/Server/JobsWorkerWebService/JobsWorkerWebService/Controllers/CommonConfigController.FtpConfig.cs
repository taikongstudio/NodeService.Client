using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/ftp/addorupdate")]
        public async Task<ApiResult<IEnumerable<FtpConfigModel>>> AddOrUpdateAsync([FromBody] FtpConfigModel[] ftpConfigs)
        {
            ApiResult<IEnumerable<FtpConfigModel>> apiResult = new ApiResult<IEnumerable<FtpConfigModel>>();
            try
            {
                foreach (var ftpConfig in ftpConfigs)
                {
                    var ftpConfigFromDb = await this._applicationDbContext.FtpConfigsDbSet.FindAsync(ftpConfig.Id);
                    if (ftpConfigFromDb == null)
                    {
                        await this._applicationDbContext.FtpConfigsDbSet.AddAsync(ftpConfig);
                    }
                    else
                    {
                        ftpConfigFromDb.With(ftpConfig);
                    }
                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.FtpConfigsDbSet;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/ftp/list")]
        public Task<ApiResult<IEnumerable<FtpConfigModel>>> QueryFtpConnectionConfigAsync()
        {
            ApiResult<IEnumerable<FtpConfigModel>> apiResult = new ApiResult<IEnumerable<FtpConfigModel>>();
            try
            {
                apiResult.Result = this._applicationDbContext.FtpConfigsDbSet;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return Task.FromResult(apiResult);
        }

        [HttpGet("/api/commonconfig/ftp/{id}")]
        public async Task<ApiResult<FtpConfigModel>> GetFtpConfigAsync(string id)
        {
            ApiResult<FtpConfigModel> apiResult = new ApiResult<FtpConfigModel>();
            try
            {
                apiResult.Result = await this._applicationDbContext.FtpConfigsDbSet.FindAsync(id);
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


        [HttpPost("/api/commonconfig/ftp/remove")]
        public async Task<ApiResult<IEnumerable<FtpConfigModel>>> RemoveAsync([FromBody] FtpConfigModel[] ftpConfigs)
        {
            ApiResult<IEnumerable<FtpConfigModel>> apiResult = new ApiResult<IEnumerable<FtpConfigModel>>();
            try
            {
                this._applicationDbContext.FtpConfigsDbSet.RemoveRange(ftpConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.FtpConfigsDbSet;
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
