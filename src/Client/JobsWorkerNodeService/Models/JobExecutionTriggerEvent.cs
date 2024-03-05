using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.MessageQueues.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobsWorkerNodeService.Models
{
    public class JobExecutionTriggerEvent : Message<JobExecutionTriggerDetails>
    {

    }
}
