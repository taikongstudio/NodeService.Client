using System;
using System.Collections.Generic;
using System.Text;

namespace JobsWorkerNode.Models
{
    public class PluginInfo
    {
        public string name { get; set; }

        public string version { get; set; }

        public string filename { get; set; }

        public string exePath { get; set; }

        public string hash { get; set; }

        public bool launch { get; set; }

        public string platform { get; set; }
    }
}
