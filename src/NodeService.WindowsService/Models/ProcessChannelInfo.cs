using NodeService.Infrastructure.Models;
using System.Threading.Channels;

namespace NodeService.WindowsService.Models
{
    public class ProcessChannelInfo
    {
        public ProcessChannelInfo(Process process)
        {
            Process = process;
            ProcessCommandChannel = Channel.CreateUnbounded<ProcessCommandRequest>();
        }

        public Process Process { get; private set; }

        public Channel<ProcessCommandRequest> ProcessCommandChannel { get; private set; }
    }
}
