using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    public partial class NodesController
    {

        [HttpGet("/api/nodes/{id}/config/template")]
        public async Task<ApiResult<NodeConfigTemplateModel>> QueryNodeConfigTemplateAsync(string id)
        {
            ApiResult<NodeConfigTemplateModel> apiResult = new ApiResult<NodeConfigTemplateModel>();
            try
            {
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(id);
                if (nodeInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = $"invalid node id:{id}";
                }
                else
                {

                    apiResult.Result = nodeInfo.ActiveNodeConfigTemplate;
                }
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }


        [HttpPost("/api/nodes/{id}/config/applytemplate")]
        public async Task<ApiResult<bool>> ApplyNodeConfigTemplateAsync(string id, [FromQuery] string templateId)
        {
            ApiResult<bool> apiResult = new ApiResult<bool>();
            try
            {
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(id);
                if (nodeInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = $"Could not found node info:{id}";
                }
                else
                {
                    var nodeConfigTemplate = await this._applicationDbContext.NodeConfigTemplateDbSet.FindAsync(templateId);
                    if (nodeConfigTemplate == null)
                    {
                        apiResult.ErrorCode = -1;
                        apiResult.Message = $"Could not found node config template:{templateId}";
                    }
                    else
                    {
                        nodeInfo.ActiveNodeConfigTemplateForeignKey = nodeConfigTemplate.Id;
                        var changes= await this._applicationDbContext.SaveChangesAsync();
                        if (changes > 0)
                        {
                            await this._nodeConfigChangedMessage.PostMessageAsync(nameof(JobScheduleService),
                                new NodeConfigTemplateNotificationMessage()
                                {
                                    Content = nodeConfigTemplate.Id,
                                    Key = Guid.NewGuid().ToString(),
                                    DateTime = DateTime.Now,
                                });
                        }
                        apiResult.Result = true;
                    }
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
