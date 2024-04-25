using NodeService.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

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
