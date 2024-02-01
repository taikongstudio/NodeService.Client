using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessStatTool
{
    public class Options
    {

        [Option("host", HelpText = "host")]
        public string host { get; set; }

        [Option("port", HelpText = "port")]
        public int port { get; set; }

        [Option("username", HelpText = "username")]
        public string username { get; set; }

        [Option("password", HelpText = "password")]
        public string password { get; set; }

        [Option("input", HelpText = "input")]
        public string input { get; set; }

        [Option("output", HelpText = "output")]
        public string output { get; set; }

        [Option("action", HelpText = "action")]
        public string action { get; set; }

        [Option("rsp", HelpText = "rsp", Required = false)]
        public string rsp { get; set; }

        [Option("args", HelpText = "args", Required = false)]
        public IEnumerable<string> args { get; internal set; }

        public Options Clone()
        {
            var clone = new Options();
            clone.host = host;
            clone.port = port;
            clone.username = username;
            clone.password = password;
            clone.input = input;
            clone.output = output;
            clone.action = action;
            clone.args = this.args.ToArray();
            return clone;

        }
    }
}
