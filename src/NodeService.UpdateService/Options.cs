
using CommandLine;
using System.Text.Json;

namespace NodeService.UpdateService
{
    public class Options
    {
        [Option("exitid", HelpText = "exitid")]
        public string exitid { get; set; }

        [Option("parentprocessid", HelpText = "parentprocessid")]
        public string parentprocessid { get; set; }


        [Option("mode", HelpText = "mode")]
        public string mode { get; set; }

        [Option("address", HelpText = "address")]
        public string address { get; set; }


        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
