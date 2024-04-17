namespace NodeService.ServiceProcess
{
    public abstract class PackageProvider
    {
        public abstract Task<bool> DownloadAsync(Stream stream, CancellationToken cancellationToken = default);
    }
}