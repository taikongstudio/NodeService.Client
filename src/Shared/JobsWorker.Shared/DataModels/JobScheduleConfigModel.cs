using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{

    public class JobScheduleConfigModel : ModelBase
    {


        [Required]
        public string Description { get; set; }

        public bool IsEnabled { get; set; }
        [Required]
        public string? JobTypeDescForeignKey {  get; set; }

        [JsonIgnore]
        public JobTypeDescConfigModel? JobTypeDesc { get; set; }

        public List<StringEntry> CronExpressions { get; set; } = [];


        public List<NodeInfoJobScheduleConfigBindingModel> NodeInfoJobScheduleConfigBindingList { get; set; } = [];


        [JsonIgnore]
        public List<NodeInfoModel> NodeInfoList { get; set; } = [];



        [JsonIgnore]
        [NotMapped]
        private JsonElement _jsonElement;

        [JsonIgnore]
        [NotMapped]
        public JsonElement optionsElement
        {
            get
            {
                if (Options == null)
                {
                    return default;
                }
                if (_jsonElement.ValueKind == JsonValueKind.Undefined)
                {
                    var jsonString = JsonSerializer.Serialize(Options);
                    var bytes = Encoding.UTF8.GetBytes(jsonString);
                    Utf8JsonReader jsonReader = new Utf8JsonReader(bytes);
                    _jsonElement = JsonElement.ParseValue(ref jsonReader);
                }
                return _jsonElement;
            }
        }

        [Required]
        public Dictionary<string, object?> Options { get; set; } = [];

        public List<StringEntry> DnsFilters { get; set; } = [];

        public string DnsFilterType { get; set; } = "exclude";

        public List<StringEntry> IpAddressFilters { get; set; } = [];

        public string IpAddressFilterType { get; set; } = "exclude";


        public List<JobScheduleConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];

        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];


        public List<JobExecutionInstanceModel> JobExecutionRecords { get; set; } = [];

        public JobScheduleConfigModel With(JobScheduleConfigModel model)
        {
            this.Id = model.Id;
            this.Name = model.Name;
            this.DnsFilters = model.DnsFilters;
            this.DnsFilterType = model.DnsFilterType;
            this.IpAddressFilters = model.IpAddressFilters;
            this.IpAddressFilterType = model.IpAddressFilterType;
            this.JobTypeDesc = model.JobTypeDesc;
            this.Properties = model.Properties;
            //this.TemplateBindingList.Update(model.TemplateBindingList);
            this.Options = model.Options;
            this.CronExpressions = model.CronExpressions;
            this.IsEnabled = model.IsEnabled;
            this.JobTypeDescForeignKey = model.JobTypeDescForeignKey;
            this.Description = model.Description;
            return this;
        }
    }
}
