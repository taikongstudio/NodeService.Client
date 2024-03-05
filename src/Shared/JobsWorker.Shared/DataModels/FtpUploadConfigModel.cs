using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{
    public partial class FtpUploadConfigModel : ModelBase
    {

        [Required]
        public string LocalDirectory { get; set; }

        public bool IsLocalDirectoryUseMapping { get; set; }
        [Required]
        public string RemoteDirectory { get; set; }
        [Required]
        public string SearchPattern { get; set; }
        [Required]
        public List<StringEntry> Filters { get; set; } = [];

        public bool IncludeSubDirectories { get; set; }


        public List<FtpUploadConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];

        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];

        public string FtpConfigForeignKey { get; set; }
        [JsonIgnore]
        public FtpConfigModel? FtpConfig { get; set; }

        public string? LocalDirectoryMappingConfigForeignKey { get; set; }
        [JsonIgnore]
        public LocalDirectoryMappingConfigModel? LocalDirectoryMappingConfig { get; set; }

        public int RetryTimes { get; set; }

        public void With(FtpUploadConfigModel model)
        {
            this.Id = model.Id;
            this.Name = model.Name;
            this.FtpConfigForeignKey = model.FtpConfigForeignKey;
            this.LocalDirectoryMappingConfigForeignKey = model.LocalDirectoryMappingConfigForeignKey;
            this.LocalDirectory = model.LocalDirectory;
            this.RemoteDirectory = model.RemoteDirectory;
            this.RetryTimes = model.RetryTimes;
            this.Filters = model.Filters;
            this.IncludeSubDirectories = model.IncludeSubDirectories;
            this.SearchPattern = model.SearchPattern;
            this.RetryTimes = model.RetryTimes;
            this.TemplateBindingList = model.TemplateBindingList;
            this.IsLocalDirectoryUseMapping = model.IsLocalDirectoryUseMapping;
        }
    }
}
