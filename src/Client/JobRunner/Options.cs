using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace JobRunner
{
    public class Options
    {

        [Option("parentProcessId", HelpText = "parentprocessid")]
        public string parentProcessId { get; set; }

        [Option("jobConfigPath", HelpText = "jobConfigPath")]
        public string jobConfigPath { get; set; }

        [Option("nodeConfigPath", HelpText = "nodeConfigPath")]
        public string nodeConfigPath { get; set; }


        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
