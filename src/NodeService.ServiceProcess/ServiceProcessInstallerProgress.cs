public enum ServiceProcessInstallerProgressType
{
    Info,
    Warning,
    Error
}

namespace NodeService.ServiceProcess
{
    public class ServiceProcessInstallerProgress
    {
        public ServiceProcessInstallerProgress(
            string serviceName,
            ServiceProcessInstallerProgressType type,
            string message)
        {
            ServiceName = serviceName;
            Type = type;
            Message = message;
        }

        public string? ClientUpdateId {  get; set; }

        public string Message { get; private set; }

        public string ServiceName { get; private set; }

        public ServiceProcessInstallerProgressType Type { get; private set; }
    }
}