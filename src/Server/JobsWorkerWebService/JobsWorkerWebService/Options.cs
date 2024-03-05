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
