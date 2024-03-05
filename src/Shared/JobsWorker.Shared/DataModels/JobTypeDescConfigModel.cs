using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public class JobTypeDescConfigModel : ModelBase
    {
        [Required]
        public string FullName { get; set; }
        [Required]
        public string Description { get; set; }

        [Required]
        public List<StringEntry> OptionEditors { get; set; } = [];

        public List<JobScheduleConfigModel> JobScheduleConfigs { get; set; } = [];



    }
}
