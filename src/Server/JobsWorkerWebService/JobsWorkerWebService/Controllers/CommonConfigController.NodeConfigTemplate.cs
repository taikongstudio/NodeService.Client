using JobsWorker.Shared.DataModels;
using static System.Formats.Asn1.AsnWriter;

namespace JobsWorkerWebService.Controllers
{
    public partial class CommonConfigController
    {


        [HttpPost("/api/commonconfig/nodeconfigtemplates/addorupdate")]
        public async Task<ApiResult<IEnumerable<NodeConfigTemplateModel>>> AddOrUpdateAsync([FromBody] NodeConfigTemplateModel[] nodeConfigTemplates)
        {
            ApiResult<IEnumerable<NodeConfigTemplateModel>> apiResult = new ApiResult<IEnumerable<NodeConfigTemplateModel>>();
            try
            {
                foreach (var nodeConfigTemplate in nodeConfigTemplates)
                {

                    var updatedModel = await this._applicationDbContext.AddOrUpdateAsync(nodeConfigTemplate);

                    int changes = await this._applicationDbContext.SaveChangesAsync();
                    if (changes > 0)
                    {
                        var messageQueue = this._serviceProvider.GetKeyedService<NodeConfigTemplateNotificationMessageQueue>(nameof(JobScheduleService));
                        await messageQueue.PostMessageAsync(nameof(JobScheduleService), new NodeConfigTemplateNotificationMessage()
                        {
                            Content = updatedModel.Id,
                            DateTime = DateTime.Now,
                            Key = Guid.NewGuid().ToString(),
                        });
                    }
                }

                apiResult = await this.QueryNodeConfigTemplatesAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/nodeconfigtemplates/list")]
        public async Task<ApiResult<IEnumerable<NodeConfigTemplateModel>>> QueryNodeConfigTemplatesAsync()
        {
            ApiResult<IEnumerable<NodeConfigTemplateModel>> apiResult = new ApiResult<IEnumerable<NodeConfigTemplateModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext.NodeConfigTemplateDbSet
                    .Include(x => x.FtpConfigTemplateBindingList)
                    .Include(x => x.FtpUploadConfigTemplateBindingList)
                    .Include(x => x.LogUploadConfigTemplateBindingList)
                    .Include(x => x.JobScheduleConfigTemplateBindingList)
                    .Include(x => x.PluginConfigTemplateBindingList)
                    .Include(x => x.MysqlConfigTemplateBindingList)
                    .Include(x => x.KafkaConfigTemplateBindingList)
                    .Include(x => x.RestApiConfigTemplateBindingList)
                    .Include(x => x.LocalDirectoryMappingConfigTemplateBindingList)
                    .Include(x => x.FtpConfigs)
                    .Include(x => x.FtpUploadConfigs)
                    .Include(x => x.LogUploadConfigs)
                    .Include(x => x.JobScheduleConfigs)
                    .Include(x => x.PluginConfigs)
                    .Include(x => x.MysqlConfigs)
                    .Include(x => x.KafkaConfigs)
                    .Include(x => x.RestApiConfigs)
                    .Include(x => x.LocalDirectoryMappingConfigs)
                    .Include(x => x.Nodes)
                    .AsSplitQuery()
                    .ToArrayAsync();
                foreach (var item in apiResult.Result)
                {
                    item.NodeIdList.AddRange(item.Nodes.Select(x => x.Id).Distinct());
                }
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


        [HttpPost("/api/commonconfig/nodeconfigtemplates/remove")]
        public async Task<ApiResult<IEnumerable<NodeConfigTemplateModel>>> RemoveAsync([FromBody] NodeConfigTemplateModel[] taskScheduleConfigs)
        {
            ApiResult<IEnumerable<NodeConfigTemplateModel>> apiResult = new ApiResult<IEnumerable<NodeConfigTemplateModel>>();
            try
            {
                this._applicationDbContext.NodeConfigTemplateDbSet.RemoveRange(taskScheduleConfigs);
                await this._applicationDbContext.SaveChangesAsync();
                apiResult.Result = this._applicationDbContext.NodeConfigTemplateDbSet;
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

        [HttpPost("/api/commonconfig/nodeconfigtemplates/setdefault")]
        public async Task<ApiResult<IEnumerable<NodeConfigTemplateModel>>> SetDefaultAsync([FromQuery] string templateId)
        {
            ApiResult<IEnumerable<NodeConfigTemplateModel>> apiResult = new ApiResult<IEnumerable<NodeConfigTemplateModel>>();
            try
            {
                var defaultNodeConfigTemplate = await this._applicationDbContext.NodeConfigTemplateDbSet.FindAsync(templateId);
                if (defaultNodeConfigTemplate == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = $"Could not found node config template:{templateId}";
                }
                else
                {
                    foreach (var nodeConfigTeamplate in this._applicationDbContext.NodeConfigTemplateDbSet)
                    {
                        nodeConfigTeamplate.IsDefault = false;
                    }
                    defaultNodeConfigTemplate.IsDefault = true;
                    foreach (var nodeInfo in this._applicationDbContext.NodeInfoDbSet.Where(x => x.ActiveNodeConfigTemplateForeignKey == null))
                    {
                        nodeInfo.ActiveNodeConfigTemplateForeignKey = defaultNodeConfigTemplate.Id;
                    }
                    await this._applicationDbContext.SaveChangesAsync();
                    apiResult.Result = this._applicationDbContext.NodeConfigTemplateDbSet;
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
