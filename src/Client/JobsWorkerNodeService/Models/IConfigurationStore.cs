
using JobsWorker.Shared.Models;

namespace JobsWorkerNodeService.Models
{
    public interface IConfigurationStore
    {
        NodeConfig NodeConfig { get; set; }


    }
}
