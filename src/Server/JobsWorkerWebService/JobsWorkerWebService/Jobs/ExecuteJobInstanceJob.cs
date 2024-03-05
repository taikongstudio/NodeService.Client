using CommandLine;
using JobsWorker.Shared;
using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.MessageQueues;
using JobsWorker.Shared.MessageQueues.Models;
using JobsWorkerWebService.Data;
using System.Threading.Tasks;
using static Google.Protobuf.WireFormat;

namespace JobsWorkerWebService.Jobs
{
    public class ExecuteJobInstanceJob : IJob
    {
        public ExecuteJobInstanceJob()
        {

        }

        public IServiceProvider ServiceProvider { get; set; }

        public JobScheduleConfigModel JobScheduleConfig { get; set; }

        public NodeConfigTemplateModel NodeConfigTemplate {  get; set; }

        public ILogger<ExecuteJobInstanceJob> Logger { get; set; }

        private static TaskCompletionSource _debugTCS;

        public async Task Execute(IJobExecutionContext context)
        {
            await ExecuteCoreAsync(context);
        }

        private async Task ExecuteCoreAsync(IJobExecutionContext context)
        {
            this.Logger.LogInformation($"Job fire instance id:{context.FireInstanceId}");
            var inprocRpc = this.ServiceProvider.GetService<IInprocRpc<string, string, RequestMessage, ResponseMessage>>();
            using var scope = this.ServiceProvider.CreateAsyncScope();
            using var applicationDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jobScheduleConfigJsonString = this.JobScheduleConfig.ToJsonString<JobScheduleConfigModel>();
            var nodeConfigTemplateJsonString = this.NodeConfigTemplate.ToJsonString<NodeConfigTemplateModel>();
            //var allNodes = await applicationDbContext.NodeInfoDbSet.ToArrayAsync();
            //var filteredInNodes = allNodes.Where(FilterNode);
            //var offlineNodes = filteredInNodes.Where(x => x.Status != NodeStatus.Offline);
            //var onlineNodes = filteredInNodes.Where(x => x.Status == NodeStatus.Online);

            foreach (var nodeInfo in this.NodeConfigTemplate.Nodes)
            {
                try
                {
                    bool isFilteredIn = FilterNode(nodeInfo);

                    bool isOnline = nodeInfo.Status == NodeStatus.Online;

                    if (!isFilteredIn)
                    {
                        continue;
                    }

                    JobExecutionInstanceModel jobExecutionInstanceModel = new JobExecutionInstanceModel
                    {
                        Id = $"{nodeInfo.Name}_{context.FireInstanceId}_{Guid.NewGuid()}",
                        Name = $"{nodeInfo.Name} {context.FireInstanceId}",
                        NodeInfoForeignKey = nodeInfo.Id,
                        Status = JobExecutionInstanceStatus.Triggered,
                        FireTime = context.FireTimeUtc.ToLocalTime().DateTime,
                        Message = "",
                        FireType = "Server"
                    };

                    if (!isOnline)
                    {
                        jobExecutionInstanceModel.Message = $"{nodeInfo.Name} offline";
                    }
                    else
                    {
                        jobExecutionInstanceModel.Message = $"{nodeInfo.Name} triggered";
                    }



                    applicationDbContext.JobExecutionInstancesDbSet.Add(jobExecutionInstanceModel);

                    if (isOnline)
                    {
                        await PostJobTriggerReqToNodeAsync(
                        jobExecutionInstanceModel.Id,
                        context,
                        inprocRpc,
                        nodeInfo,
                        nodeConfigTemplateJsonString,
                        jobScheduleConfigJsonString
                        );
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.ToString());
                }

            }
            await applicationDbContext.SaveChangesAsync();


        }

        private async Task PostJobTriggerReqToNodeAsync(
            string jobInstanceId,
            IJobExecutionContext context,
            IInprocRpc<string, string, RequestMessage, ResponseMessage>? inprocRpc,
            NodeInfoModel nodeInfo,
            string nodeConfigTemplateJsonString,
            string jobScheduleConfigJsonString
            )
        {
            var taskExecutionTriggerReq = new JobExecutionTriggerReq()
            {
                NodeName = nodeInfo.Name,
                RequestId = Guid.NewGuid().ToString(),
            };
            taskExecutionTriggerReq.Properties.Add(ConfigurationKeys.JobScheduleConfig, jobScheduleConfigJsonString);
            taskExecutionTriggerReq.Properties.Add(ConfigurationKeys.NodeConfigTemplate, nodeConfigTemplateJsonString);
            taskExecutionTriggerReq.Properties.Add(nameof(context.FireInstanceId), context.FireInstanceId);
            taskExecutionTriggerReq.Properties.Add(nameof(JobExecutionInstanceModel.Id), jobInstanceId);
            if (context.PreviousFireTimeUtc != null)
            {
                taskExecutionTriggerReq.Properties.Add(nameof(context.PreviousFireTimeUtc), context.PreviousFireTimeUtc.Value.ToLocalTime().DateTime.ToString());
            }

            if (context.NextFireTimeUtc != null)
            {
                taskExecutionTriggerReq.Properties.Add(nameof(context.NextFireTimeUtc), context.NextFireTimeUtc.Value.ToLocalTime().DateTime.ToString());
            }

            taskExecutionTriggerReq.Properties.Add(nameof(context.ScheduledFireTimeUtc), context.ScheduledFireTimeUtc?.ToLocalTime().DateTime.ToString());
            await inprocRpc.PostAsync(taskExecutionTriggerReq.NodeName, new JobExecutionTriggerRequest()
            {
                Content = taskExecutionTriggerReq,
                DateTime = DateTime.Now,
                Key = taskExecutionTriggerReq.RequestId,
                Timeout = TimeSpan.FromSeconds(30)
            });
        }

        private bool FilterNode(NodeInfoModel nodeInfoModel)
        {
            switch (this.JobScheduleConfig.DnsFilterType)
            {
                case "include":
                    return DnsFilterIncludeNode(nodeInfoModel);
                case "exclude":
                    return DnsFilterExcludeNode(nodeInfoModel);
                default:
                    return false;
            }
        }

        private bool DnsFilterIncludeNode(NodeInfoModel nodeInfo)
        {
            return this.JobScheduleConfig.DnsFilters.Any(x => x.Value == nodeInfo.Name);
        }

        private bool DnsFilterExcludeNode(NodeInfoModel nodeInfo)
        {
            return !this.JobScheduleConfig.DnsFilters.Any(x => x.Value == nodeInfo.Name);
        }

        private bool NoFilter(NodeInfoModel nodeInfo)
        {
            return true;
        }

    }
}
