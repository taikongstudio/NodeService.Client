namespace NodeService.ServiceProcess
{
    public class FtpPackageProvider : PackageProvider, IDisposable
    {
        private const int Timeout = 60000;

        private readonly AsyncFtpClient _ftpClient;
        private readonly string _filePath;
        private IProgress<FtpProgress> _progressProvider;

        public FtpPackageProvider(
            string host,
            string username,
            string password,
            string filePath,
            IProgress<FtpProgress> progressProvider,
            int port = 21)
        {
            _ftpClient = new AsyncFtpClient(host, username, password, port, new FtpConfig()
            {
                ConnectTimeout = Timeout,
                ReadTimeout = Timeout,
                DataConnectionConnectTimeout = Timeout,
                DataConnectionReadTimeout = Timeout
            });
            _filePath = filePath;
            _progressProvider = progressProvider;
        }

        public void Dispose()
        {
            _ftpClient.Dispose();
        }

        public override async Task<bool> DownloadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                await _ftpClient.AutoConnect();
                if (await _ftpClient.FileExists(_filePath) == false)
                {
                    return false;
                }

                return await _ftpClient.DownloadStream(stream, _filePath, progress: _progressProvider);
            }
            catch (Exception ex)
            {

            }
            return false;
        }
    }
}
