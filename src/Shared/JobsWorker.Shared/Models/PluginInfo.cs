using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace JobsWorker.Shared.Models
{
    [Table("plugin_info")]
    public class PluginInfo
    {
        [Key]
        public string pluginId { get; set; }

        public string pluginName { get; set; }

        public string version { get; set; }

        public string downloadUrl { get; set; }

        public string entryPoint { get; set; }

        public string arguments { get; set; }

        public string hash { get; set; }

        public bool launch { get; set; }

        public string platform { get; set; }
    }
}
