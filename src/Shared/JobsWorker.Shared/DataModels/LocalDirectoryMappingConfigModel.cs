using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{

    public class LocalDirectoryMappingConfigModel : ModelBase
    {

        public bool IsDefault { get; set; }

        [Required]
        public List<StringEntry> Entries { get; set; } = new List<StringEntry>();

        public List<LocalDirectoryMappingConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];

        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];

        public List<FtpUploadConfigModel> FtpUploadConfigs { get; set; } = [];


        public StringEntry AddEntry()
        {
            StringEntry entry = new StringEntry();
            entry.Id = Guid.NewGuid().ToString();
            this.Entries.Add(entry);
            return entry;
        }

        public void With(LocalDirectoryMappingConfigModel localDirectoryMappingConfig)
        {
            this.Id = localDirectoryMappingConfig.Id;
            this.Name = localDirectoryMappingConfig.Name;
            this.Entries = localDirectoryMappingConfig.Entries;
            this.IsDefault = localDirectoryMappingConfig.IsDefault;
        }
    }
}
