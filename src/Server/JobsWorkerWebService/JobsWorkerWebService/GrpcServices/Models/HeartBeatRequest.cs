using JobsWorker.Shared;
using JobsWorker.Shared.MessageQueues.Models;

namespace JobsWorkerWebService.GrpcServices.Models
{
    public class HeartBeatRequest : RequestMessage<HeartBeatReq>
    {

    }
}
