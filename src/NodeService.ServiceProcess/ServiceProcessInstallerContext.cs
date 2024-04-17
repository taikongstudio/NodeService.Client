namespace NodeService.ServiceProcess
{
    public class ServiceProcessInstallContext
    {
        public ServiceProcessInstallContext(string serviceName, string displayName, string description, string installDirectory)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            Description = description;
            InstallDirectory = installDirectory;
        }

        public string InstallDirectory { get; set; }

        public string ServiceName { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

    }
}