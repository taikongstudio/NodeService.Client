using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{
    public class PluginConfigModel : ModelBase
    {
        [Required]
        public string Version { get; set; }

        public string? DownloadUrl { get; set; }

        public string? EntryPoint { get; set; }
        public string? Arguments { get; set; }
        public string? Hash { get; set; }

        public bool Launch { get; set; }
        [Required]
        public string Platform { get; set; }

        public string? FileName { get; set; }

        public long FileSize { get; set; }


        public List<PluginConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];

        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];

        public PluginConfigModel With(PluginConfigModel model)
        {
            this.Arguments = model.Arguments;
            this.DownloadUrl = model.DownloadUrl;
            this.EntryPoint = model.EntryPoint;
            this.FileName = model.FileName;
            this.FileSize = model.FileSize;
            this.Hash = model.Hash;
            this.Launch = model.Launch;
            this.Name = model.Name;
            this.Platform = model.Platform;
            this.Id = model.Id;
            this.Version = model.Version;
            this.TemplateBindingList = model.TemplateBindingList;
            return this;
        }
    }
}
