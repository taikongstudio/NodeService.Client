
using JobsWorker.Shared.DataModels;
using System.Collections.Concurrent;

namespace JobsWorkerNodeService.Models
{
    public interface IConfigurationStore
    {
        CancellationTokenSource GrpcLoopCancellationTokenSource { get; set; }

        string ActiveGrpcAddress { get; set; }

    }
}
