using JobsWorker.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public class NodePropertySnapshotModel : ModelBase
    {
        [Required]
        public string GroupForeignKey { get; set; }

        [JsonIgnore]
        public NodePropertySnapshotGroupModel Group { get; set; }


        [Required]
        public List<NodePropertyEntry> NodeProperties { get; set; } = [];

        public DateTime CreationDateTime { get; set; }


    }

}
