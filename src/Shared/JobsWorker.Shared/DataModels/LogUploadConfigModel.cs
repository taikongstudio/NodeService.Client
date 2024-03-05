using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{

    public class LogUploadConfigModel : ModelBase
    {

        [Required]
        public List<StringEntry> LocalDirectories { get; set; } = new List<StringEntry>();

        public bool IsLocalDirectoryUseMapping { get; set; }

        [Required]
        public string RemoteDirectory { get; set; }

        public string? SearchPattern { get; set; }

        public bool IncludeSubDirectories { get; set; }

        public long SizeLimitInBytes { get; set; }

        public long TimeLimitInSeconds { get; set; }

        [Required]
        public string FtpConfigForeignKey { get; set; }

        [JsonIgnore]
        public FtpConfigModel? FtpConfig { get; set; }

        public List<LogUploadConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];

        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];


        public void With(LogUploadConfigModel logUploadConfig)
        {
            this.Id = logUploadConfig.Id;
            this.Name = logUploadConfig.Name;
            this.SearchPattern = logUploadConfig.SearchPattern;
            this.FtpConfigForeignKey = logUploadConfig.FtpConfigForeignKey;
            this.FtpConfig = logUploadConfig.FtpConfig;
            this.RemoteDirectory = logUploadConfig.RemoteDirectory;
            this.LocalDirectories = logUploadConfig.LocalDirectories;
            this.IncludeSubDirectories = logUploadConfig.IncludeSubDirectories;
            this.SizeLimitInBytes = logUploadConfig.SizeLimitInBytes;
            this.TimeLimitInSeconds = logUploadConfig.TimeLimitInSeconds;
        }
    }
}
