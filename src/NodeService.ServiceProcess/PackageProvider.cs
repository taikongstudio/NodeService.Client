namespace NodeService.ServiceProcess
{
    public abstract class PackageProvider
    {
        public abstract Task<bool> DownloadAsync(Stream destStream, CancellationToken cancellationToken = default);
    }
}