using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace MaccorUploadTool
{
    public class Options
    {
        [Option("ConfigFilePath", Required = true, HelpText = "ConfigFilePath")]
        public string ConfigFilePath { get; set; }


        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
