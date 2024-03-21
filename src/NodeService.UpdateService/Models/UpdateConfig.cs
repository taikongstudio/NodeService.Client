using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.UpdateService.Models
{
    public class UpdateConfig
    {
        public string HttpAddress { get; set; }

        public string InstallDirectory { get; set; }

        public int DurationMinutes { get; set; }
    }
}
