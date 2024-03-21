using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftptool
{
    public class MachineDirectoryMappingConfig
    {
        public hostConfig[] hostConfigs { get; set; }

        public bool TryGetHostConfig(string hostName, out hostConfig? hostConfig)
        {

            hostConfig = this.hostConfigs == null ? null : this.hostConfigs.FirstOrDefault(x => x.host == hostName);
            return hostConfig != null;
        }

    }
}
