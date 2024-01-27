using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MaccorUploadTool.Models
{
    /// <summary>
    /// 数据上传服务配置
    /// </summary>
    public class DataReportServiceConfig
    {
        public string path { get; set; }

        public string broker_list { get; set; }

        public string header_topic_name { get; set; }

        public string time_data_topic_name { get; set; }

        public string path_resolve_file { get; set; }

        public int maxDegreeOfParallelism { get; set; }

        public string dbName { get; set; }

        public string AsJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public static bool TryLoad(string configPath, out DataReportServiceConfig dataReportServiceConfig)
        {
            dataReportServiceConfig = null;

            try
            {
                dataReportServiceConfig = JsonSerializer.Deserialize<DataReportServiceConfig>(File.ReadAllText(configPath));
                return true;
            }
            catch (Exception ex)
            {

            }
            return false;
        }


    }
}
