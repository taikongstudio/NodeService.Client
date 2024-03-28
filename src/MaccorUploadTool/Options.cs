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
        [Option("Directory", Required = true, HelpText = "Directory")]
        public string Directory { get; set; }

        [Option("KafkaBrokerList", Required = true, HelpText = "KafkaBrokerList")]
        public string KafkaBrokerList { get; set; }

        [Option("NodeId", Required = true, HelpText = "NodeId")]
        public string NodeId { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
