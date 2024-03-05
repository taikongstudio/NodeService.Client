using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Models
{
    public class DetectProcessAndClickButtonJobOptions
    {
        public string processName { get; set; }

        public string buttonText { get; set; }

        public string exePath { get; set; }

        public string workingDirectory { get; set; }
    }
}
