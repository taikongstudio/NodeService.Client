using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class MySqlConfig
    {
        public string configName { get; set; }
        public string mysql_host { get; set; }

        public string mysql_userid { get; set; }

        public string mysql_password { get; set; }

        public string mysql_database { get; set; }

        public string AsJson()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
