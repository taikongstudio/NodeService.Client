namespace NodeService.ServiceProcess
{
    public class ServiceProcessRecoveryContext
    {
        public string HttpAddress { get; set; }

        public string InstallDirectory { get; set; }

        public string ServiceName { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string Arguments {  get; set; }

        public int DurationMinutes { get; set; }
    }
}