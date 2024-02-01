using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerWebService.Models.Configurations
{
    public class FtpServerConfig
    {
        public string name { get; set; }

        public string host { get; set; }

        public int port { get; set; }

        public string username { get; set; }

        public string password { get; set; }

        public string rootDirectory { get; set; }
    }
}
