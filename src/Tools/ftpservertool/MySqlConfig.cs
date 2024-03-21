using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ftpservertool
{
    public class MySqlConfig
    {
        public int Index { get; set; }
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
