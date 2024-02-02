using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerWebService
{
    public class Options
    {
        [Option("env", HelpText = "env")]
        public string env { get; set; }

        [Option("urls", HelpText = "urls")]
        public string urls { get; set; }

    }
}
