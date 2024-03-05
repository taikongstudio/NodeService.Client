using JobsWorker.Shared.DataModels;
using System.Collections.Concurrent;

namespace JobsWorkerNodeService.Models
{
    public class ConfigurationStore : IConfigurationStore
    {
        public ConfigurationStore()
        {

        }

        public NodeConfigTemplateModel ActiveNodeConfigTemplate { get; set; }
        public CancellationTokenSource GrpcLoopCancellationTokenSource { get; set; }
        public string ActiveGrpcAddress { get; set; }
        public bool IsNodeConfigInstalling { get; set; }


    }
}
