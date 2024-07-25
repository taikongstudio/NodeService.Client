namespace NodeService.WindowsService.Models
{
    public class ServiceOptions
    {


        [Option("mode", HelpText = "mode")]
        public string mode { get; set; }

        [Option("env", Default = nameof(Environments.Production), HelpText = "env")]
        public string env { get; set; }

        [Option("doctor", Default = false, HelpText = "doctor")]
        public bool doctor { get; set; }


        [Option("waitfordebugger", Default = false, HelpText = "waitfordebugger")]
        public bool waitfordebugger { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
