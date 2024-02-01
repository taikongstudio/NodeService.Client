using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class FtpTaskConfig
    {
        public string taskName { get; set; }

        public string ftpConfig { get; set; }

        public string localDirectory { get; set; }

        public bool isLocalDirectoryUseMapping { get; set; }

        public string remoteDirectory { get; set; }

        public string searchPattern { get; set; }

        public string[] filters { get; set; }

        public bool includeSubDirectories { get; set; }

        public string directoryConfigName { get; set; }

        public int retryTimes { get; set; }

        public string directoryMappingConfigPath { get; set; }

    }
}
