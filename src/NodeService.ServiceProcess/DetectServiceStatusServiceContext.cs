using System.Collections.Concurrent;

namespace NodeService.ServiceProcess
{
    public class DetectServiceStatusServiceContext
    {
        public DetectServiceStatusServiceContext()
        {
            this.RecoveryContexts = new ConcurrentDictionary<string, ServiceProcessDoctor>();
        }

        public IDictionary<string, ServiceProcessDoctor> RecoveryContexts { get; private set; }

    }

}
