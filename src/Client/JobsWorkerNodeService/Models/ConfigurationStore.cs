using JobsWorker.Shared.Models;

namespace JobsWorkerNodeService.Models
{
    public class ConfigurationStore : IConfigurationStore
    {
        public ConfigurationStore()
        {

        }
        public NodeConfig NodeConfig { get; set; }
    }
}
