using NodeService.Infrastructure.Concurrent;
using System.Collections.Concurrent;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {
        readonly IAsyncQueue<FileSystemWatchEventReport> _fileSystemWatchEventQueue;

    }
}
