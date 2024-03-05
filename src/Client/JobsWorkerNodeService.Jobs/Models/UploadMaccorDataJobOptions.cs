using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Models
{
    public class UploadMaccorDataJobOptions
    {
        public string path { get; set; }
        public string broker_list { get; set; }
        public string header_topic_name { get; set; }
        public string time_data_topic_name { get; set; }
        public int maxDegreeOfParallelism { get; set; }
        public string mysqlConfigName { get; set; }
        public string dbName { get; set; }
        public string plugin_path { get; set; }

    }
}
