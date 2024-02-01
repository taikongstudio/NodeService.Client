using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class FtpConnectConfig
    {
        public string configName { get; set; }

        public string host { get; set; }

        public int port { get; set; }

        public string username { get; set; }

        public string password { get; set; }

        public string defaultWorkingDirectory {  get; set; }
    }
}
