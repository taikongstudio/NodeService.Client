using System.ComponentModel.DataAnnotations;

namespace JobsWorkerWebService.Models
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
