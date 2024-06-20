using CommandLine;

namespace NodeService.DeviceHost.Models
{
    public class ServiceOptions
    {

        [Option("mode", HelpText = "Mode")]
        public string? mode { get; set; }

        [Option("env", HelpText = "Env")]
        public string? env { get; set; }

        [Option("pid", HelpText = "pid")]
        public string? pid { get; set; }
    }
}
