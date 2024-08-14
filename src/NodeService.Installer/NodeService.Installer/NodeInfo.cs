using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.Installer
{
    public class NodeInfo
    {
        public bool IsSelected { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string LastOnlineTime { get; set; }

        public string Usages { get; set; }

        public bool IsNew { get; set; }
    }
}
