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
    public class NodeProfileModel : ModelBase
    {

        public string? FactoryName { get; set; }


        public string? TestInfo { get; set; }

        public string? LabArea { get; set; }

        public string? LabName { get; set; }

        public string? LoginName { get; set; }

        public bool InstallStatus { get; set; }

        public DateTime UpdateTime { get; set; }

        public string? ClientVersion { get; set; }

        public string? Usages { get; set; }

        public string? Remarks { get; set; }

        public string? IpAddress { get; set; }
    }
}
