using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerWebService.Client
{
    public class MachineInfo
    {
        public int id { get; set; }
        public string? factory_name { get; set; }

        public string? host_name { get; set; }

        public string? test_info { get; set; }

        public string? lab_area { get; set; }

        public string? lab_name { get; set; }

        public string? computer_name { get; set; }

        public string? login_name { get; set; }

        public bool? install_status { get; set; }

        public string? update_time { get; set; }

        public string? version { get; set; }

        public string? usages { get; set; }

        public string? remarks { get; set; }

        public string? ip_addresses { get; set; }

        public bool? has_ftp_dir { get; set; }

    }
}
