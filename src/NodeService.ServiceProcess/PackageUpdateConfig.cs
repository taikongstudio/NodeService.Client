namespace NodeService.ServiceProcess
{
    public class PackageUpdateConfig
    {
        public string HttpAddress { get; set; }

        public string InstallDirectory { get; set; }

        public string ServiceName { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public int DurationMinutes { get; set; }
    }
}