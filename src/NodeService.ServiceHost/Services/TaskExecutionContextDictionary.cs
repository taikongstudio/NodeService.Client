using System.Collections.Concurrent;

namespace NodeService.ServiceHost.Services
{
    public class TaskExecutionContextDictionary : ConcurrentDictionary<string, TaskExecutionContext>
    {

    }
}
