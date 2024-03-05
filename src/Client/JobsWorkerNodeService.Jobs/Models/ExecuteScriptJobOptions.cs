using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Models
{
    public class ExecuteScriptJobOptions
    {
        public string scripts {  get; set; }

        public string workingDirectory { get; set; }

        public bool createNoWindow {  get; set; }

    }
}
