using System.IO;
using System.Security.Cryptography;

namespace NodeService.ServiceProcess
{
    public class HttpPackageProvider : PackageProvider
    {
        private readonly ApiService _apiService;
        private readonly PackageConfigModel _packageConfig;

        public HttpPackageProvider(ApiService apiService, PackageConfigModel packageConfig)
        {
            _apiService = apiService;
            _packageConfig = packageConfig;
        }

        public override async Task<bool> DownloadAsync(Stream destStream, CancellationToken cancellationToken = default)
        {
            try
            {
                var rsp = await _apiService.DownloadPackageAsync(_packageConfig, cancellationToken);
                if (rsp.ErrorCode == 0 && rsp.Result != null)
                {
                    using var srcStream = rsp.Result;
                    srcStream.Position = 0;
                    var bytes = await SHA256.HashDataAsync(srcStream).ConfigureAwait(false);
                    var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                    srcStream.Position = 0;
                    if (hash == _packageConfig.Hash)
                    {
                        await srcStream.CopyToAsync(destStream);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return false;
        }
    }
}