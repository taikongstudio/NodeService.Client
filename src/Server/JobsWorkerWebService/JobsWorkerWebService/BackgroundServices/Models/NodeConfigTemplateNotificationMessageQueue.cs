
using JobsWorker.Shared.MessageQueues;

namespace JobsWorkerWebService.BackgroundServices.Models
{
    public class NodeConfigTemplateNotificationMessageQueue :
        InprocMessageQueue<string, string, NodeConfigTemplateNotificationMessage>
    {

    }
}
