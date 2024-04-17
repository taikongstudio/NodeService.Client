using System;
using System.Collections.Generic;
using System.Text;

public abstract class PackageProvider
{
    public abstract Task<bool> DownloadAsync(Stream stream, CancellationToken cancellationToken = default);
}