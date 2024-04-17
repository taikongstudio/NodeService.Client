using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Text;

public class HttpPackageProvider : PackageProvider
{
    private readonly ApiService _apiService;
    private readonly PackageConfigModel _packageConfig;

    public HttpPackageProvider(ApiService apiService, PackageConfigModel packageConfig)
    {
        _apiService = apiService;
        _packageConfig = packageConfig;
    }


    public override async Task<bool> DownloadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            await _apiService.DownloadPackageAsync(_packageConfig, stream, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {

        }
        return false;
    }
}