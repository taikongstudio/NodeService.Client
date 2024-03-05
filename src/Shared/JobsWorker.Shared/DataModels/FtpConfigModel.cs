using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{

    public class FtpConfigModel : ModelBase
    {

        [Required]
        public string Host { get; set; }

        public int Port { get; set; } = 21;
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }

        public string? DefaultWorkingDirectory { get; set; }

        public int ConnectTimeout { get; set; } = 60000;
        public int ReadTimeout { get; set; } = 60000;
        public int DataConnectionReadTimeout { get; set; } = 60000;
        public int DataConnectionConnectTimeout { get; set; } = 60000;

        [JsonIgnore]
        public List<FtpConfigTemplateBindingModel> TemplateBindingList { get; set; } = [];

        [JsonIgnore]
        public List<NodeConfigTemplateModel> Templates { get; set; } = [];

        [JsonIgnore]
        public List<FtpUploadConfigModel> FtpUploadConfigBindingList { get; set; } = [];

        [JsonIgnore]
        public List<LogUploadConfigModel> LogUploadConfigBindingList { get; set; } = [];

        public void With(FtpConfigModel model)
        {
            this.Id = model.Id;
            this.Name = model.Name;
            this.Host = model.Host;
            this.Port = model.Port;
            this.Username = model.Username;
            this.Password = model.Password;
            this.DefaultWorkingDirectory = model.DefaultWorkingDirectory;
            this.ConnectTimeout = model.ConnectTimeout;
            this.DataConnectionConnectTimeout = model.DataConnectionConnectTimeout;
            this.ReadTimeout = model.ReadTimeout;
            this.DataConnectionReadTimeout = model.DataConnectionReadTimeout;
            this.TemplateBindingList = model.TemplateBindingList;
            this.FtpUploadConfigBindingList = model.FtpUploadConfigBindingList;
        }

    }
}
