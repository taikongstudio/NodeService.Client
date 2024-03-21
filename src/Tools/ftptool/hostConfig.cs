using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftptool
{
    public class hostConfig
    {

        public string host {  get; set; }

        public hostDirectoryConfig[] configs { get; set; }

        public bool TryGetDirectoryConfig(string configName, out hostDirectoryConfig? result)
        {
            result = this.configs.FirstOrDefault(x => x.name == configName);
            return result != null;
        }
        
    }
}
