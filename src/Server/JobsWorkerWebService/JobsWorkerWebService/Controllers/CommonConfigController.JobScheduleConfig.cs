using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/taskschedule/addorupdate")]
        public async Task<ApiResult<IEnumerable<JobScheduleConfigModel>>> AddOrUpdateAsync([FromBody] JobScheduleConfigModel[] taskScheduleConfigs)
        {
            ApiResult<IEnumerable<JobScheduleConfigModel>> apiResult = new ApiResult<IEnumerable<JobScheduleConfigModel>>();
            try
            {
                foreach (var taskScheduleConfig in taskScheduleConfigs)
                {
                    var updatedModel = await this._applicationDbContext.AddOrUpdateAsync(taskScheduleConfig);

                    this._applicationDbContext.ChangeTracker.DetectChanges();
                    Console.WriteLine(this._applicationDbContext.ChangeTracker.DebugView.LongView);

                    int changes = await this._applicationDbContext.SaveChangesAsync();
                    if (changes > 0)
                    {
                        foreach (var template in updatedModel.Templates)
                        {
                            var messageQueue = this._serviceProvider.GetKeyedService<NodeConfigTemplateNotificationMessageQueue>(nameof(JobScheduleService));
                            await messageQueue.PostMessageAsync(nameof(JobScheduleService), new NodeConfigTemplateNotificationMessage()
                            {
                                Content = template.Id,
                                DateTime = DateTime.Now,
                                Key = Guid.NewGuid().ToString(),
                            });
                        }

                    }
                }
                apiResult = await this.QueryTaskScheduleConfigAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/taskschedule/list")]
        public async Task<ApiResult<IEnumerable<JobScheduleConfigModel>>> QueryTaskScheduleConfigAsync()
        {
            ApiResult<IEnumerable<JobScheduleConfigModel>> apiResult = new ApiResult<IEnumerable<JobScheduleConfigModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext.JobScheduleConfigsDbSet
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


        [HttpPost("/api/commonconfig/taskschedule/remove")]
        public async Task<ApiResult<IEnumerable<JobScheduleConfigModel>>> RemoveAsync([FromBody] JobScheduleConfigModel[] taskScheduleConfigs)
        {
            ApiResult<IEnumerable<JobScheduleConfigModel>> apiResult = new ApiResult<IEnumerable<JobScheduleConfigModel>>();
            try
            {
                this._applicationDbContext.JobScheduleConfigsDbSet.RemoveRange(taskScheduleConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await this.QueryTaskScheduleConfigAsync();
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
