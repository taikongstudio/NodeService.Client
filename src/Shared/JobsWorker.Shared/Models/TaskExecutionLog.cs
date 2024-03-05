using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class JobExecutionLog
    {
        public string NodeName { get; set; }

        public string InstanceId { get; set; }

        public string LogPath { get; set; }

        public List<string> Logs { get; set; }
    }
}
