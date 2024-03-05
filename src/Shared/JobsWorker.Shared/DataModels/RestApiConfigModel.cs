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
    [PrimaryKey(nameof(Id))]
    public class RestApiConfigModel : ModelBase
    {
        [Required]
        public string Description { get; set; }

        [Required]
        public string Name { get; set; }
        [Required]
        public string RequestUri { get; set; }

        public List<RestApiConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];


        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];

        public RestApiConfigModel With(RestApiConfigModel model)
        {
            this.Id = model.Id;
            this.Description = model.Description;
            this.RequestUri = model.RequestUri;
            this.Name = model.Name;
            this.TemplateBindingList = model.TemplateBindingList;
            return this;
        }

    }
}
