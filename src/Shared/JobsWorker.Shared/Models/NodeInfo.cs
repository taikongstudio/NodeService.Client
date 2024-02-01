using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.Models
{
    [Table("node_info")]
    public class NodeInfo
    {
        public string node_id { get; set; }
        [Key]
        public string? node_name { get; set; }

        public string? factory_name { get; set; }
        [NotMapped]
        public string? host_name { get; set; }

        public string? test_info { get; set; }

        public string? lab_area { get; set; }

        public string? lab_name { get; set; }

        public string? login_name { get; set; }

        public bool? install_status { get; set; }

        public string? update_time { get; set; }

        public string? version { get; set; }

        public string? usages { get; set; }

        public string? remarks { get; set; }

        public string? ip_addresses { get; set; }

        public bool? has_ftp_dir { get; set; }

        public string? node_config { get; set; }

    }
}
