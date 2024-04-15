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

        [Option("KafkaBrokerList", Required = false, HelpText = "KafkaBrokerList")]
        public string KafkaBrokerList { get; set; }

        [Option("NodeId", Required = true, HelpText = "NodeId")]
        public string NodeId { get; set; }

        [Option("ParentProcessId", Required = true, HelpText = "ParentProcessId")]
        public string ParentProcessId { get; set; }


        [Option("DateTime", Required = true, HelpText = "DateTime")]
        public string DateTime { get; set; }


        [Option("Mode", Required = false, HelpText = "Mode")]
        public string Mode {  get; set; }

        [Option("FtpConfigId", Required = false, HelpText = "FtpConfigId")]
        public string FtpConfigId { get; set; }

        [Option("debugger", Required = false, HelpText = "debugger")]
        public string debugger { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
