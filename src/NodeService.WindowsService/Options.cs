using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace NodeService.WindowsService
{
    public class Options
    {


        [Option("mode", HelpText = "mode")]
        public string mode { get; set; }

        [Option("env", Default = nameof(Environments.Production), HelpText = "env")]
        public string env { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
