using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ftptool
{
    public class FtpTaskConfig
    {
        public string taskName { get; set; }

        public string host { get; set; }

        public int port { get; set; }

        public string username { get; set; }

        public string password { get; set; }

        public string localDirectory { get; set; }

        public bool isLocalDirectoryUseMapping { get; set; }

        public string remoteDirectory { get; set; }

        public string searchPattern { get; set; }

        public string[] filters { get; set; }

        public bool includeSubDirectories { get; set; }

        public string nextTaskName { get; set; }

        public string directoryConfigName { get; set; }

       

    }
}
