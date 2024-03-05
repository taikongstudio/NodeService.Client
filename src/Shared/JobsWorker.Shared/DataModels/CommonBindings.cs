using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{

    public abstract class BindingModelBase
    {
        [JsonIgnore]
        public (string OwnerForeignKey, string TargetForeignKey) CombinedKey
        {
            get { return (this.OwnerForeignKey, this.TargetForeignKey); }
        }

        public string CombinedKeyString
        {
            get
            {
                return string.Join(",", this.OwnerForeignKey, this.TargetForeignKey);
            }
        }

        [Required]
        public string TargetForeignKey { get; set; }
        [Required]
        public string OwnerForeignKey { get; set; }

        public DateTime? PublicationDate { get; set; }
    }

    public abstract class BindingModelBase<TOwner, TTarget> : BindingModelBase
        where TOwner : ModelBase
        where TTarget : ModelBase
    {


        [JsonIgnore]
        public TOwner? Owner { get; set; }

        [JsonIgnore]
        public TTarget? Target { get; set; }

        [JsonIgnore]
        public string TargetName
        {
            get
            {
                if (Target == null)
                {
                    return string.Empty;
                }
                return Target.Name;
            }
        }
    }

    public abstract class TemplateBindingModelBase<TTarget> : BindingModelBase<NodeConfigTemplateModel, TTarget>
    where TTarget : ModelBase
    {


    }

    public abstract class NodeBindingModelBase<TTarget> : BindingModelBase<NodeInfoModel, TTarget>
    where TTarget : ModelBase
    {

    }

    public class FtpConfigTemplateBindingModel : TemplateBindingModelBase<FtpConfigModel>
    {

    }

    public class FtpUploadConfigTemplateBindingModel : TemplateBindingModelBase<FtpUploadConfigModel>
    {

    }

    public class LocalDirectoryMappingConfigTemplateBindingModel : TemplateBindingModelBase<LocalDirectoryMappingConfigModel>
    {


    }

    public class PluginConfigTemplateBindingModel : TemplateBindingModelBase<PluginConfigModel>
    {

    }

    public class RestApiConfigTemplateBindingModel : TemplateBindingModelBase<RestApiConfigModel>
    {

    }

    public class MysqlConfigTemplateBindingModel : TemplateBindingModelBase<MysqlConfigModel>
    {

    }

    public class JobScheduleConfigTemplateBindingModel : TemplateBindingModelBase<JobScheduleConfigModel>
    {


    }

    public class LogUploadConfigTemplateBindingModel : TemplateBindingModelBase<LogUploadConfigModel>
    {



    }

    public class KafkaConfigTemplateBindingModel : TemplateBindingModelBase<KafkaConfigModel>
    {



    }

    public class NodeConfigTemplateNodeInfoBindingModel : TemplateBindingModelBase<NodeInfoModel>
    {
        public bool IsActive { get; set; }
    }


    public class NodeInfoActiveNodeConfigTeplateBindingModel : NodeBindingModelBase<NodeConfigTemplateModel>
    {

    }

    public class NodeInfoNodePropertySnapshotBindingModel : NodeBindingModelBase<NodePropertySnapshotModel>
    {

    }

}
