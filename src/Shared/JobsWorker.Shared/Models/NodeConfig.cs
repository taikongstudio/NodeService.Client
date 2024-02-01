using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace JobsWorker.Shared.Models
{
    public class NodeConfig
    {
        public string name { get; set; }

        public string[] addresses { get; set; }

        public string version { get; set; }

        public PluginInfo[] plugins { get; set; }

        public JobScheduleConfig[] jobScheduleConfigs { get; set; }

        public FtpConnectConfig[] ftpServerConfigs { get; set; }

        public LogUploadConfig[] logUploadConfigs { get; set; }

        public MySqlConfig[] mysqlConfigs { get; set; }

        public RestApi[] restApis { get; set; }

        public FtpTaskConfig[] ftpTaskConfigs { get; set; }

        public string AsJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
