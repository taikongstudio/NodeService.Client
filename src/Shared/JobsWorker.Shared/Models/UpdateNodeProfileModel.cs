using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class UpdateNodeProfileModel
    {
        [Required]
        public string NodeId { get; set; }
        public string? Remarks { get; set; }
        public string? Usages { get; set; }
        public string? LabName { get; set; }
        public string? LabArea { get; set; }
        public string? TestInfo { get; set; }
    }
}
