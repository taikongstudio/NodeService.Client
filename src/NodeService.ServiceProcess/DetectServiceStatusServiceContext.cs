using System.Collections.Concurrent;

namespace NodeService.ServiceProcess
{
    public class DetectServiceStatusServiceContext
    {
        public DetectServiceStatusServiceContext()
        {
            this.RecoveryContexts = new ConcurrentDictionary<string, ServiceProcessRecovey>();
        }

        public IDictionary<string, ServiceProcessRecovey> RecoveryContexts { get; private set; }

    }

}
