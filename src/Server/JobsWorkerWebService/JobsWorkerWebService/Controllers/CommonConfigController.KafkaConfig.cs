using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/kafka/addorupdate")]
        public async Task<ApiResult<IEnumerable<KafkaConfigModel>>> AddOrUpdateAsync([FromBody] KafkaConfigModel[] kafkaConfigs)
        {
            ApiResult<IEnumerable<KafkaConfigModel>> apiResult = new ApiResult<IEnumerable<KafkaConfigModel>>();
            try
            {
                foreach (var kafkaConfig in kafkaConfigs)
                {
                    var kafkaConfigFromDb = await this._applicationDbContext.KafkaConfigsDbSet.FindAsync(kafkaConfig.Id);
                    if (kafkaConfigFromDb == null)
                    {
                        await this._applicationDbContext
                            .KafkaConfigsDbSet
                            .AddAsync(kafkaConfig);
                    }
                    else
                    {
                        kafkaConfigFromDb.With(kafkaConfig);
                    }
                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.KafkaConfigsDbSet;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/kafka/list")]
        public Task<ApiResult<IEnumerable<KafkaConfigModel>>> QueryKafkaConfigAsync()
        {
            ApiResult<IEnumerable<KafkaConfigModel>> apiResult = new ApiResult<IEnumerable<KafkaConfigModel>>();
            try
            {
                apiResult.Result = this._applicationDbContext.KafkaConfigsDbSet;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return Task.FromResult(apiResult);
        }

        [HttpPost("/api/commonconfig/kafka/remove")]
        public async Task<ApiResult<IEnumerable<KafkaConfigModel>>> RemoveAsync([FromBody] KafkaConfigModel[] kafkaConfigs)
        {
            ApiResult<IEnumerable<KafkaConfigModel>> apiResult = new ApiResult<IEnumerable<KafkaConfigModel>>();
            try
            {
                this._applicationDbContext.KafkaConfigsDbSet.RemoveRange(kafkaConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.KafkaConfigsDbSet;
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
