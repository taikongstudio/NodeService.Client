using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftptool
{
    public class Response
    {
        public string machineDirectoryMappingConfigPath { get; set; }

        public FtpTaskConfig[] ftpTasks { get; set; }

        public string defaultTaskName { get; set; }


    }
}
