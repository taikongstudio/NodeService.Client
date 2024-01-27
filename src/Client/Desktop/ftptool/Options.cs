using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftptool
{
    public class Options
    {

        [Option("rsp", HelpText = "rsp", Required = false)]
        public string rsp { get; set; }
    }
}
