using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public class KafkaConfigModel : ModelBase
    {

        [Required]
        public string BrokerList { get; set; }


        public List<StringEntry> Topics { get; set; } = [];

        public List<KafkaConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];

        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];

        public void With(KafkaConfigModel model)
        {
            this.Id = model.Id;
            this.Name = model.Name;
            this.BrokerList = model.BrokerList;
            this.Topics = model.Topics;
        }
    }
}
