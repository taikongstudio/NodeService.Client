using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/ftpupload/addorupdate")]
        public async Task<ApiResult<IEnumerable<FtpUploadConfigModel>>>
            UpdateLocalDirectoryConfigAsync([FromBody] FtpUploadConfigModel[] ftpUploadConfigs)
        {
            ApiResult<IEnumerable<FtpUploadConfigModel>> apiResult = new ApiResult<IEnumerable<FtpUploadConfigModel>>();
            try
            {
                foreach (var ftpUploadConfig in ftpUploadConfigs)
                {
                    var ftpUploadConfigFromDb = await this._applicationDbContext.FtpUploadConfigsDbSet.FindAsync(ftpUploadConfig.Id);
                    if (ftpUploadConfigFromDb == null)
                    {
                        await this._applicationDbContext.FtpUploadConfigsDbSet.AddAsync(ftpUploadConfig);
                    }
                    else
                    {
                        ftpUploadConfigFromDb.With(ftpUploadConfig);
                    }
                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await this.QueryFtpUploadConfigAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/ftpupload/list")]
        public async Task<ApiResult<IEnumerable<FtpUploadConfigModel>>> QueryFtpUploadConfigAsync()
        {
            ApiResult<IEnumerable<FtpUploadConfigModel>> apiResult = new ApiResult<IEnumerable<FtpUploadConfigModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext
                    .FtpUploadConfigsDbSet
                    .Include(x => x.FtpConfig)
                    .Include(x => x.LocalDirectoryMappingConfig)
                    .ToArrayAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


        [HttpPost("/api/commonconfig/ftpupload/remove")]
        public async Task<ApiResult<IEnumerable<FtpUploadConfigModel>>> RemoveAsync([FromBody] FtpUploadConfigModel[] ftpUploadConfigs)
        {
            ApiResult<IEnumerable<FtpUploadConfigModel>> apiResult = new ApiResult<IEnumerable<FtpUploadConfigModel>>();
            try
            {
                this._applicationDbContext.FtpUploadConfigsDbSet.RemoveRange(ftpUploadConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.FtpUploadConfigsDbSet;
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
