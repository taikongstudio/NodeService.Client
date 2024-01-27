using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorkerWebService.Server.Models
{
    public class FtpServerConfig
    {
        public string server { get; set; }

        public string password { get; set; }

        public string username { get; set; }

        public string localLogDirectory { get; set; }



        public string remoteLogDirectoryFormat { get; set; }

        public string[] uploadLogJobCronExpressions { get; set; }

        public string version { get; set; }

        public string[] configfiles { get; set; }

        public PluginInfo[] plugins { get; set; }

        public int SleepSeconds { get; set; }

        public string AsJsonString()
        {
            return JsonSerializer.Serialize(this);
        }




    }
}
