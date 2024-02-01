using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class LogUploadConfig
    {
        public string configName { get; set; }

        public string[] localLogDirectories { get; set; }

        public string remoteLogDirectoryFormat { get; set; }

        public string searchPattern { get; set; }

        public long sizeLimit { get; set; }

        public int timeLimit { get; set; }

        public string ftpConfigName { get; set; }
    }
}
