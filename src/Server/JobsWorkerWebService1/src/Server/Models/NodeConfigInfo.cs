using System.ComponentModel.DataAnnotations;

namespace JobsWorkerWebService.Server.Models
{
    public class NodeConfigInfo
    {
        [Required]
        [Key]
        public string config_id { get; set; }
        [Required]
        public string node_name { get; set; }
        [Required]
        public string node_config { get; set; }
        [Required]
        public string channel { get; set; }
    }
}
