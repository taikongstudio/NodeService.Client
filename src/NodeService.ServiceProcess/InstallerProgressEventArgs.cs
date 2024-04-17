namespace NodeService.ServiceProcess
{
    public class InstallerProgressEventArgs : EventArgs
    {
        public InstallerProgressEventArgs(ServiceProcessInstallerProgress progress)
        {
            Progress = progress;
        }

        public ServiceProcessInstallerProgress Progress { get; private set; }
    }
}