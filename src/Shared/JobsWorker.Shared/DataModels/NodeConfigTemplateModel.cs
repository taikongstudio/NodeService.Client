using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{

    public class NodeConfigTemplateModel : ModelBase
    {


        [Required]
        public string Description { get; set; }

        public DateTime? ModifiedDateTime { get; set; }

        public bool IsDefault { get; set; }

        [Required]
        public string HttpAddress { get; set; }
        [Required]
        public string GrpcAddress { get; set; }
        [Required]
        public string Version { get; set; }

        public List<PluginConfigTemplateBindingModel> PluginConfigTemplateBindingList { get; set; } = [];

        public List<JobScheduleConfigTemplateBindingModel> JobScheduleConfigTemplateBindingList { get; set; } = [];

        public List<FtpConfigTemplateBindingModel> FtpConfigTemplateBindingList { get; set; } = [];

        public List<LogUploadConfigTemplateBindingModel> LogUploadConfigTemplateBindingList { get; set; } = [];

        public List<MysqlConfigTemplateBindingModel> MysqlConfigTemplateBindingList { get; set; } = [];

        public List<RestApiConfigTemplateBindingModel> RestApiConfigTemplateBindingList { get; set; } = [];

        public List<FtpUploadConfigTemplateBindingModel> FtpUploadConfigTemplateBindingList { get; set; } = [];

        public List<LocalDirectoryMappingConfigTemplateBindingModel> LocalDirectoryMappingConfigTemplateBindingList { get; set; } = [];

        public List<KafkaConfigTemplateBindingModel> KafkaConfigTemplateBindingList { get; set; } = [];

        //[NotMapped]
        //public List<NodeConfigTemplateNodeInfoBindingModel> NodeInfoTemplateBindingList { get; set; } = [];


        public List<JobScheduleConfigModel> JobScheduleConfigs { get; set; } = [];



        public List<FtpConfigModel> FtpConfigs { get; set; } = [];



        public List<FtpUploadConfigModel> FtpUploadConfigs { get; set; } = [];




        public List<LogUploadConfigModel> LogUploadConfigs { get; set; } = [];



        public List<KafkaConfigModel> KafkaConfigs { get; set; } = [];


        public List<MysqlConfigModel> MysqlConfigs { get; set; } = [];



        public List<LocalDirectoryMappingConfigModel> LocalDirectoryMappingConfigs { get; set; } = [];


        public List<RestApiConfigModel> RestApiConfigs { get; set; } = [];



        public List<PluginConfigModel> PluginConfigs { get; set; } = [];

        [Required]
        public List<NodeInfoModel> Nodes { get; set; } = [];

        [NotMapped]
        public List<string> NodeIdList { get; set; } = [];

        public MysqlConfigModel? FindMysqlConfig(string id)
        {
            return MysqlConfigTemplateBindingList.FirstOrDefault(x => x.TargetForeignKey == id)?.Target;
        }

        public FtpConfigModel? FindFtpConfig(string id)
        {
            return FtpConfigTemplateBindingList.FirstOrDefault(x => x.TargetForeignKey == id)?.Target;
        }

        public LogUploadConfigModel? FindLogUploadConfig(string id)
        {
            return LogUploadConfigTemplateBindingList.FirstOrDefault(x => x.TargetForeignKey == id)?.Target;
        }

        public FtpUploadConfigModel? FindFtpUploadConfig(string id)
        {
            return FtpUploadConfigTemplateBindingList.FirstOrDefault(x => x.TargetForeignKey == id)?.Target;
        }

        public PluginConfigModel? FindPluginConfig(string id)
        {
            return PluginConfigTemplateBindingList.FirstOrDefault(x => x.TargetForeignKey == id)?.Target;
        }

        public LocalDirectoryMappingConfigModel? FindLocalDirectoryMappingConfig(string id)
        {
            return LocalDirectoryMappingConfigTemplateBindingList.FirstOrDefault(x => x.TargetForeignKey == id)?.Target;
        }


        protected override void UpdateProperties()
        {
            foreach (var binding in this.JobScheduleConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.FtpUploadConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.FtpConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.MysqlConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.LogUploadConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.PluginConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.RestApiConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.LocalDirectoryMappingConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
            foreach (var binding in this.KafkaConfigTemplateBindingList)
            {
                binding.OwnerForeignKey = this.Id;
            }
        }



    }
}
