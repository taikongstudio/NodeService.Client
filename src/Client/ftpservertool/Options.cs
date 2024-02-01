using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftpservertool
{
    public class Options
    {
        [Option("host", HelpText = "ftp host", Required = false)]
        public string host { get; set; }

        [Option("port", HelpText = "port", Required = false)]
        public int port { get; set; }

        [Option("username", HelpText = "username", Required = false)]
        public string username { get; set; }


        [Option("password", HelpText = "password", Required = false)]
        public string password { get; set; }

        [Option("action", HelpText = "action", Required = false)]
        public string action { get; set; }

        [Option("args", HelpText = "args", Required = false, Separator = ';')]
        public IEnumerable<string> args { get; set; }


        [Option("rsp", HelpText = "rsp", Required = false)]
        public string rsp { get; set; }
    }
}
