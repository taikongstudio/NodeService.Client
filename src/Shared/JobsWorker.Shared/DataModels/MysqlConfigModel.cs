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
    public class MysqlConfigModel : ModelBase
    {
        [Required]
        public string Host { get; set; }

        public int Port { get; set; } = 3306;
        [Required]
        public string UserId { get; set; }
        [Required]
        public string Password { get; set; }
        [Required]
        public string Database { get; set; }
        public List<MysqlConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];


        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];

        public void With(MysqlConfigModel mysqlConfig)
        {
            this.Id = mysqlConfig.Id;
            this.Name = mysqlConfig.Name;
            this.Host = mysqlConfig.Host;
            this.Port = mysqlConfig.Port;
            this.UserId = mysqlConfig.UserId;
            this.Password = mysqlConfig.Password;
            this.Database = mysqlConfig.Database;
        }
    }
}
