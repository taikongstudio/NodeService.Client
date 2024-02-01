using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorUploadTool
{
    public class Options
    {

        [Option("parentprocessid", HelpText = "parentprocessid")]
        public string ParentProcesssId { get; set; }


        

        



    }
}
