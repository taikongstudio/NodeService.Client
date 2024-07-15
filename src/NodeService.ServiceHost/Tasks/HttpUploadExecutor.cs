using FluentFTP;
using NodeService.Infrastructure.NodeFileSystem;
using System.Collections.Immutable;

namespace NodeService.ServiceHost.Tasks
{
    public class HttpUploadExecutor
    {
        int _uploadedCount = 0;
        int _skippedCount = 0;
        int _overidedCount = 0;
        readonly IHttpClientFactory _httpClientFactory;
        readonly string _nodeInfoId;
        ImmutableDictionary<string, string?> _envVars;
        readonly string _contextId;
        readonly string _siteUrl;

        public FtpUploadConfigModel FtpUploadConfiguration { get; private set; }

        public ILogger _logger { get; set; }


        public HttpUploadExecutor(
            string nodeInfoId,
            string contextId,
            string siteUrl,
            FtpUploadConfigModel ftpTaskConfig,
            IHttpClientFactory httpClientFactory,
            ILogger logger)
        {
            _contextId = contextId;
            _siteUrl = siteUrl;
            FtpUploadConfiguration = ftpTaskConfig;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _nodeInfoId = nodeInfoId;
        }

        public void SetEnvironmentVariables(ImmutableDictionary<string, string?> envVars)
        {
            _envVars = envVars;
            foreach (var envVar in envVars)
            {
                _logger.LogInformation($"\"{envVar.Key}\" => \"{envVar.Value}\"");
            }
        }

        string GetLocalDirectory()
        {
            return FtpUploadConfiguration.LocalDirectory;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Execute config on host {FtpUploadConfiguration.FtpConfig.Host} port {FtpUploadConfiguration.FtpConfig.Port}" +
                $" username {FtpUploadConfiguration.FtpConfig.Username} password {FtpUploadConfiguration.FtpConfig.Password}");

            var hostName = Dns.GetHostName();


            string rootDirectory = GetLocalDirectory();

            if (!Directory.Exists(rootDirectory))
            {
                _logger.LogInformation($"Could not found directory:{rootDirectory}");
                return;
            }

            _logger.LogInformation("Start enumerate files");
            List<string> filePathList = [];
            if (FtpUploadConfiguration.UseFileGlobbing)
            {
                StringComparison stringComparison = StringComparison.OrdinalIgnoreCase;
                switch (FtpUploadConfiguration.Value.MatchCasing)
                {
                    case MatchCasing.PlatformDefault:
                        if (!OperatingSystem.IsWindows())
                        {
                            stringComparison = StringComparison.Ordinal;
                        }
                        break;
                    case MatchCasing.CaseSensitive:
                        stringComparison = StringComparison.Ordinal;
                        break;
                    case MatchCasing.CaseInsensitive:
                        stringComparison = StringComparison.OrdinalIgnoreCase;
                        break;
                    default:
                        break;
                }
                var fileGlobbingMatcher = new FileGlobbingMatcher(stringComparison);
                List<string> includes = [];
                List<string> excludes = [];
                foreach (var item in FtpUploadConfiguration.Value.FileGlobbingPatterns)
                {
                    if (!Enum.TryParse(item.Name, true, out FilePathFilter pathFilter))
                        continue;
                    switch (pathFilter)
                    {
                        case FilePathFilter.Contains:
                            includes.Add(item.Value);
                            break;
                        case FilePathFilter.NotContains:
                            excludes.Add(item.Value);
                            break;
                        default:
                            break;
                    }
                }
                filePathList = fileGlobbingMatcher.Match(
                    rootDirectory,
                    includes,
                    excludes,
                    FtpUploadConfiguration.DateTimeFilters,
                    FtpUploadConfiguration.LengthFilters).ToList();

            }
            else
            {
                var enumerator = new NodeFileSystemEnumerator(FtpUploadConfiguration);
                filePathList = enumerator.EnumerateAllFiles().ToList();
            }

            if (filePathList.Count == 0)
            {
                _logger.LogInformation($"Cound not found any mached file in {rootDirectory}");
                return;
            }

            PrintLocalFiles(filePathList);

            FtpUploadConfiguration.RemoteDirectory = FtpUploadConfiguration.RemoteDirectory.Replace("$(HostName)", hostName).Replace("\\", "/");

            if (!FtpUploadConfiguration.RemoteDirectory.StartsWith('/'))
            {
                FtpUploadConfiguration.RemoteDirectory = '/' + FtpUploadConfiguration.RemoteDirectory;
            }

            var remoteFilePathList = PathHelper.CalculateRemoteFilePath(
                FtpUploadConfiguration.LocalDirectory,
                FtpUploadConfiguration.RemoteDirectory,
                filePathList).ToList();
            if (remoteFilePathList.Count != filePathList.Count)
            {
                throw new InvalidOperationException("file count error");
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_siteUrl);
            using var apiService = new ApiService(httpClient, new ApiServiceOptions()
            {
                DisposeHttpClient = true,
            });

            PrintRemoteFiles();

            await Parallel.ForEachAsync(remoteFilePathList,
                new ParallelOptions()
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = 4
                }
                , async (kv, token) =>
              {
                  await UploadFileAsync(
                          apiService,
                          kv.Key,
                          kv.Value,
                          token);
              });

            _logger.LogInformation($"UploadedCount:{_uploadedCount} SkippedCount:{_skippedCount} OveridedCount:{_overidedCount}");

        }

        async Task UploadFileAsync(
            ApiService apiService,
            string localFilePath,
            string remoteFilePath,
            CancellationToken cancellationToken)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;
            int retryTimes = 0;
            do
            {
                ftpStatus = await UploadFileCoreAsync(
                    apiService,
                    localFilePath,
                    remoteFilePath,
                    cancellationToken);
                retryTimes++;
                if (ftpStatus == FtpStatus.Failed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }

            } while (!cancellationToken.IsCancellationRequested
                &&
                ftpStatus == FtpStatus.Failed
                &&
                (FtpUploadConfiguration.RetryTimes == 0 ||

                FtpUploadConfiguration.RetryTimes > 0
                &&
                retryTimes <= FtpUploadConfiguration.RetryTimes
                ));

            if (ftpStatus == FtpStatus.Success)
            {
                _uploadedCount++;
                _logger.LogInformation($"Upload:LocalPath:{localFilePath} RemotePath:{remoteFilePath}");
            }
            else if (ftpStatus == FtpStatus.Skipped)
            {
                _skippedCount++;
                _logger.LogInformation($"Skip:LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
            }
        }

        void PrintRemoteFiles()
        {
            if (!FtpUploadConfiguration.PrintRemoteFiles)
            {
                return;
            }
            _logger.LogInformation("Enumerate Ftp objects Begin");

            _logger.LogInformation("Enumerate Ftp objects End");
        }

        void PrintLocalFiles(IEnumerable<string> localFiles)
        {
            if (!FtpUploadConfiguration.PrintLocalFiles)
            {
                return;
            }
            _logger.LogInformation("Enumerate local Objects Begin");
            foreach (var localFile in localFiles)
            {
                var fileInfo = new FileInfo(localFile);
                _logger.LogInformation($"FullName:{fileInfo.FullName},ModifiedTime:{fileInfo.LastWriteTime},Length:{fileInfo.Length}");
            }
            _logger.LogInformation("Enumerate local Objects End");
        }

        async Task<FtpStatus> UploadFileCoreAsync(
            ApiService apiService,
            string localFilePath,
            string remoteFilePath,
            CancellationToken cancellationToken = default)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;

            bool uploadSharingViolationFile = false;
        LRetry:
            try
            {
                if (!File.Exists(localFilePath))
                {
                    return FtpStatus.Skipped;
                }
                var ftpRemoteExists = ConvertFtpFileExistsToFtpRemoteExists(FtpUploadConfiguration.FtpFileExists);

                var fileInfo = new FileInfo(localFilePath);
                var request = CreateSyncRequest(
                    _nodeInfoId,
                    FtpUploadConfiguration.FtpConfigId,
                    remoteFilePath,
                    fileInfo);

                var hitTestRsp = await apiService.HittestFileAsync(request, cancellationToken);
                if (hitTestRsp.ErrorCode == 0)
                {
                    if (hitTestRsp.Result == FileInfoCacheResult.Cached)
                    {
                        ftpStatus = FtpStatus.Skipped;
                        return ftpStatus;
                    }
                }

                using var stream = fileInfo.OpenRead();

                var rsp = await apiService.UploadFileAsync(request, stream, cancellationToken);
                if (rsp.ErrorCode == 0)
                {
                    if (rsp.Result.Status == NodeFileSyncStatus.Processed)
                    {
                        ftpStatus = FtpStatus.Success;
                    }
                    else if (rsp.Result.Status == NodeFileSyncStatus.Canceled || rsp.Result.Status == NodeFileSyncStatus.Skipped)
                    {
                        ftpStatus = FtpStatus.Skipped;
                    }
                    else if (rsp.Result.Status == NodeFileSyncStatus.Faulted)
                    {
                        ftpStatus = FtpStatus.Failed;
                    }

                }
                else
                {
                    ftpStatus = FtpStatus.Failed;
                    _logger.LogError(rsp.Message);
                }


                if (ftpStatus == FtpStatus.Success)
                {
                    if (ftpRemoteExists == FtpRemoteExists.Overwrite)
                    {
                        _overidedCount++;
                    }
                }

            }
            catch (IOException ex) when ((ex.HResult & 0x0000FFFF) == 32)
            {
                _logger.LogError(ex.ToString());
                uploadSharingViolationFile = true;
                goto LRetry;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.ToString());
            }

            return ftpStatus;
        }

        static async Task<string?> HashAsync(
           IHashAlgorithmProvider hashAlgorithmProvider,
           Stream stream,
           CancellationToken cancellationToken = default)
        {
            string? hash = null;
            if (hashAlgorithmProvider != null)
            {
                var hashBytes = await hashAlgorithmProvider.HashAsync(stream, cancellationToken);
                hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            return hash;
        }

        NodeFileSyncRequest CreateSyncRequest(
           string nodeInfoId,
           string configId,
           string storagePath,
           FileInfo fileInfo)
        {
            var req = NodeFileSyncRequestBuilder.FromFileInfo(
                                        fileInfo,
                                        nodeInfoId,
                                        _contextId,
                                        configId,
                                        NodeFileSyncConfigurationProtocol.Ftp,
                                        storagePath);
            return req;
        }

        FtpRemoteExists ConvertFtpFileExistsToFtpRemoteExists(FileExists ftpFileExists)
        {
            switch (ftpFileExists)
            {
                case FileExists.NoCheck:
                    return FtpRemoteExists.NoCheck;
                case FileExists.ResumeNoCheck:
                    return FtpRemoteExists.ResumeNoCheck;
                case FileExists.AddToEndNoCheck:
                    return FtpRemoteExists.AddToEndNoCheck;
                case FileExists.Skip:
                    return FtpRemoteExists.Skip;
                case FileExists.Overwrite:
                    return FtpRemoteExists.Overwrite;
                case FileExists.Resume:
                    return FtpRemoteExists.Resume;
                case FileExists.AddToEnd:
                    return FtpRemoteExists.AddToEnd;
                default:
                    break;
            }
            return FtpRemoteExists.Skip;
        }

        bool DiffFileInfo(string localFilePath, NodeFileInfo remoteFileInfo)
        {
            if (remoteFileInfo == null)
            {
                return true;
            }
            var localFileInfo = new FileInfo(localFilePath);
            if (!localFileInfo.Exists)
            {
                return true;
            }
            bool diffSize = DiffSize(localFileInfo, remoteFileInfo) != 0;
            bool diffFileTime = DiffFileTime(localFileInfo, remoteFileInfo);
            _logger.LogInformation($"{nameof(DiffFileInfo)}:Local:{localFileInfo.FullName}=>Remote:{remoteFileInfo.FullName}" +
                $" {nameof(DiffSize)}:{diffSize} Local:{localFileInfo.Length} Remote:{remoteFileInfo.Length}" +
                $" {nameof(DiffFileTime)}:{diffFileTime}  Local:{localFileInfo.LastWriteTime} Remote:{remoteFileInfo.LastWriteTime}");
            return diffSize && diffFileTime;
        }

        long DiffSize(FileInfo localFileInfo, NodeFileInfo remoteFileInfo)
        {
            return localFileInfo.Length - remoteFileInfo.Length;
        }

        bool DiffFileTime(FileInfo localFileInfo, NodeFileInfo remoteFileInfo)
        {
            var lastWriteTime = localFileInfo.LastWriteTime;
            bool compareDateTime = false;
            TimeSpan timeSpan = TimeSpan.MinValue;

            switch (FtpUploadConfiguration.FileExistsTimeUnit)
            {
                case FileTimeUnit.Seconds:
                    timeSpan = TimeSpan.FromSeconds(FtpUploadConfiguration.FileExistsTime);
                    break;
                case FileTimeUnit.Minutes:
                    timeSpan = TimeSpan.FromMinutes(FtpUploadConfiguration.FileExistsTime);
                    break;
                case FileTimeUnit.Hours:
                    timeSpan = TimeSpan.FromHours(FtpUploadConfiguration.FileExistsTime);
                    break;
                case FileTimeUnit.Days:
                    timeSpan = TimeSpan.FromDays(FtpUploadConfiguration.FileExistsTime);
                    break;
                default:
                    break;
            }
            switch (FtpUploadConfiguration.FileExistsTimeRange)
            {
                case CompareOperator.LessThan:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.LastWriteTime - lastWriteTime).TotalSeconds) < (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.GreatThan:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.LastWriteTime - lastWriteTime).TotalSeconds) > (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.LessThanEqual:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.LastWriteTime - lastWriteTime).TotalSeconds) <= (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.GreatThanEqual:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.LastWriteTime - lastWriteTime).TotalSeconds) >= (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.Equals:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.LastWriteTime - lastWriteTime).TotalSeconds) == (int)timeSpan.TotalSeconds;
                    break;
                default:
                    break;
            }
            return compareDateTime;
        }





    }
}
