using JobsWorker.Shared.DataModels;

namespace JobsWorkerNodeService.Models
{
    public class JobExecutionTriggerDetails
    {
        public NodeConfigTemplateModel NodeConfigTemplate { get; set; }

        public JobScheduleConfigModel JobScheduleConfig { get; set; }
    }
}
