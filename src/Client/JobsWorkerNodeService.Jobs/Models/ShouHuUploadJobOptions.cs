using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Models
{
    public class ShouHuUploadJobOptions
    {
        public string Kafka_BootstrapServers { get; set; }
        public string swj_flag { get; set; }
        public string swj_vendor { get; set; }
        public string swj_other_info { get; set; }
        public string swj_ips_regex { get; set; }
        public string swj_software_regex { get; set; }
        public string windows_service_name { get; set; }
        public string check_window_warning_time { get; set; }

    }
}
