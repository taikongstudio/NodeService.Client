using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNode
{
    public class Options
    {
        [Option("exitid", HelpText = "exitid")]
        public string exitid { get; set; }

        [Option("parentprocessid", HelpText = "parentprocessid")]
        public string parentprocessid { get; set; }

    }
}
