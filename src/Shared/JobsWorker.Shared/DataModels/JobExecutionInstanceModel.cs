using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public class JobExecutionInstanceModel : ModelBase
    {
        [Required]

        public string NodeInfoForeignKey { get; set; }

        [JsonIgnore]

        public NodeInfoModel NodeInfo { get; set; }



        public DateTime FireTime { get; set; }

        public DateTime? ExecutionBeginTime { get; set; }

        public DateTime? ExecutionEndTime { get; set; }
        [Required]
        public JobExecutionInstanceStatus Status { get; set; }
        [Required]
        public string FireType { get; set; }

        public string? LogPath { get; set; }

        public string? Message { get; set; }

    }
}
