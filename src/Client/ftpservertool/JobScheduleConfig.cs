using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftpservertool
{
    public class JobScheduleConfig
    {
        public string jobName { get; set; }

        public string[] cronExpressions { get; set; }

        public Dictionary<string, string> arguments { get; set; }

        public bool isEnabled { get; set; }

        public bool executeNow { get; set; }

    }
}
