
using CommandLine;
using System.Text.Json;

namespace NodeService.UpdateService
{
    public class Options
    {
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
