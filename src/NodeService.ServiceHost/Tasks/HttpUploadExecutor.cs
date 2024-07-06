using FluentFTP;
using FluentFTP.Proxy.AsyncProxy;
using NLog.Filters;
using NodeService.Infrastructure.NodeFileSystem;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

namespace NodeService.ServiceHost.Tasks
{
    public class HttpUploadExecutor
    {
        int _uploadedCount = 0;
        int _skippedCount = 0;
        int _overidedCount = 0;
        string? _fileSystemWatchPath;
        string? _fileSystemWatchRelativePath;
        string? _fileSystemWatchEventLocalDirectory;
        string[] _fileSystemWatchEventPathList = [];
        ConcurrentDictionary<string, NodeFileInfo> _remoteFileListDict;
        readonly IProgress<FtpProgress> _progress;
        readonly IHttpClientFactory _httpClientFactory;
        private readonly string _nodeInfoId;
        ImmutableDictionary<string, string?> _envVars;

        public FtpUploadConfigModel FtpUploadConfig { get; private set; }

        public ILogger _logger { get; set; }


        public HttpUploadExecutor(
            string nodeInfoId,
            FtpUploadConfigModel ftpTaskConfig,
            IHttpClientFactory httpClientFactory,
            ILogger logger)
        {
            FtpUploadConfig = ftpTaskConfig;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _nodeInfoId = nodeInfoId;
            _remoteFileListDict = new ConcurrentDictionary<string, NodeFileInfo>();
        }

        public void SetEnvironmentVariables(ImmutableDictionary<string, string?> envVars)
        {
            _envVars = envVars;
            foreach (var envVar in envVars)
            {
                _logger.LogInformation($"\"{envVar.Key}\" => \"{envVar.Value}\"");
            }
            if (envVars.TryGetValue(nameof(FileSystemWatchConfiguration.Path), out var path))
            {
                _fileSystemWatchPath = path;
            }
            if (envVars.TryGetValue(nameof(FileSystemWatchConfiguration.RelativePath), out var relativePath))
            {
                _fileSystemWatchRelativePath = relativePath;
            }
            if (envVars.TryGetValue(nameof(FtpUploadConfiguration.LocalDirectory), out var localDirectory))
            {
                _fileSystemWatchEventLocalDirectory = localDirectory;
            }
            if (envVars.TryGetValue("PathList", out var pathListJson))
            {
                var pathList = JsonSerializer.Deserialize<IEnumerable<string>>(pathListJson);
                _fileSystemWatchEventPathList = pathList.Select(Path.GetFullPath).ToArray();
            }
        }

        string GetLocalDirectory()
        {
            if (_fileSystemWatchEventLocalDirectory != null)
            {
                return _fileSystemWatchEventLocalDirectory;
            }
            return FtpUploadConfig.LocalDirectory;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Execute config on host {FtpUploadConfig.FtpConfig.Host} port {FtpUploadConfig.FtpConfig.Port}" +
                $" username {FtpUploadConfig.FtpConfig.Username} password {FtpUploadConfig.FtpConfig.Password}");

            var hostName = Dns.GetHostName();


            string rootDirectory = GetLocalDirectory();

            if (!Directory.Exists(rootDirectory))
            {
                _logger.LogInformation($"Could not found directory:{rootDirectory}");
                return;
            }

            _logger.LogInformation("Start enumerate files");
            List<string> filePathList = [];
            if (FtpUploadConfig.UseFileGlobbing)
            {
                StringComparison stringComparison = StringComparison.OrdinalIgnoreCase;
                switch (FtpUploadConfig.Value.MatchCasing)
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
                foreach (var item in FtpUploadConfig.Value.FileGlobbingPatterns)
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
                    FtpUploadConfig.Value.DateTimeFilters,
                    FtpUploadConfig.Value.LengthFilters).ToList();

            }
            else
            {
                var enumerator = new NodeFileSystemEnumerator(FtpUploadConfig);
                filePathList = enumerator.EnumerateAllFiles().ToList();
            }


            if (_fileSystemWatchEventPathList != null && _fileSystemWatchEventPathList.Length != 0)
            {
                filePathList = filePathList.Intersect(_fileSystemWatchEventPathList).ToList();
            }

            if (filePathList.Count == 0)
            {
                _logger.LogInformation($"Cound not found any mached file in {rootDirectory}");
                return;
            }

            PrintLocalFiles(filePathList);

            FtpUploadConfig.RemoteDirectory = FtpUploadConfig.RemoteDirectory.Replace("$(HostName)", hostName).Replace("\\", "/");

            if (!FtpUploadConfig.RemoteDirectory.StartsWith('/'))
            {
                FtpUploadConfig.RemoteDirectory = '/' + FtpUploadConfig.RemoteDirectory;
            }

            var remoteFilePathList = PathHelper.CalculateRemoteFilePath(
                FtpUploadConfig.LocalDirectory,
                FtpUploadConfig.RemoteDirectory,
                filePathList).ToList();
            if (remoteFilePathList.Count != filePathList.Count)
            {
                throw new InvalidOperationException("file count error");
            }
            _remoteFileListDict.Clear();
            using var apiService = new ApiService(_httpClientFactory.CreateClient());
            var visitedDirectoryList = new List<string>();
            foreach (var fileGroup in remoteFilePathList.GroupBy(static kv => PathHelper.GetFilePathDirectoryName(kv.Value)))
            {
                var remoteDirectory = PathHelper.AltDirectorySeperator(fileGroup.Key);
                var rsp = await apiService.QueryNodeFileObjectAsync(new QueryNodeFileSystemObjectInfoParameters()
                {
                    NodeName = hostName,
                    FilePathList = fileGroup.Select(x => x.Key).ToList()
                }, cancellationToken);
                if (rsp.ErrorCode==0)
                {
                    foreach (var item in rsp.Result)
                    {
                        this._remoteFileListDict.TryAdd(item.FullName, item);
                    }

                }
            }

            PrintRemoteFiles();

            foreach (var kv in remoteFilePathList)
            {
                await UploadFileAsync(
                    apiService,
                    kv.Key,
                    kv.Value,
                    cancellationToken);
            }

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
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }

            } while (!cancellationToken.IsCancellationRequested
                &&
                ftpStatus == FtpStatus.Failed
                &&
                (FtpUploadConfig.RetryTimes == 0 ||

                FtpUploadConfig.RetryTimes > 0
                &&
                retryTimes <= FtpUploadConfig.RetryTimes
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
            if (!FtpUploadConfig.PrintRemoteFiles)
            {
                return;
            }
            _logger.LogInformation("Enumerate Ftp objects Begin");
            foreach (var item in _remoteFileListDict)
            {
                _logger.LogInformation($"FullName:{item.Key},ModifiedTime:{item.Value.LastWriteTime},Length:{item.Value.Length}");
            }
            _logger.LogInformation("Enumerate Ftp objects End");
        }

        void PrintLocalFiles(IEnumerable<string> localFiles)
        {
            if (!FtpUploadConfig.PrintLocalFiles)
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
                FtpRemoteExists ftpRemoteExists = ConvertFtpFileExistsToFtpRemoteExists(FtpUploadConfig.FtpFileExists);
                //if (uploadSharingViolationFile)
                //{
                //    ftpStatus = await UploadSharingViolationFileAsync(apiService,
                //        localFilePath,
                //        remoteFilePath,
                //        ftpRemoteExists,
                //        cancellationToken);
                //    return ftpStatus;
                //}

                if (FtpUploadConfig.CleanupRemoteDirectory
                    || (_remoteFileListDict.TryGetValue(
                    remoteFilePath,
                    out var ftpListItem)
                    &&
                    !DiffFileInfo(
                    localFilePath,
                    ftpListItem)))
                {
                    ftpRemoteExists = FtpRemoteExists.Skip;
                }
                var fileInfo = new FileInfo(localFilePath);
                using var stream = fileInfo.OpenRead();
                var request = await CreateSyncRequest(_nodeInfoId,
                    FtpUploadConfig.FtpConfigId,
                    remoteFilePath,
                   fileInfo
                    );
                var rsp = await apiService.NodeFileUploadFileAsync(request, stream, cancellationToken);
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

        async ValueTask<NodeFileSyncRequest> CreateSyncRequest(
            string nodeInfoId,
            string configId,
            string storagePath,
            FileInfo fileInfo,
            CancellationToken cancellationToken = default)
        {
            var req = await NodeFileSyncRequestBuilder.FromFileInfoAsync(
                                        nodeInfoId,
                                        configId,
                                        NodeFileSyncConfigurationProtocol.Ftp,
                                        storagePath,
                                        fileInfo,
                                        new DefaultSHA256HashAlgorithmProvider(),
                                        new DefaultGzipCompressionProvider(),
                                        null);
            return req;
        }
        //async Task<FtpStatus> UploadSharingViolationFileAsync(
        //    ApiService apiService,
        //    string localFilePath,
        //    string remoteFilePath,
        //    FtpRemoteExists ftpRemoteExists,
        //    CancellationToken cancellationToken = default)
        //{
        //    using var fileStream = File.Open(
        //        localFilePath,
        //        FileMode.Open,
        //        FileAccess.Read,
        //        FileShare.ReadWrite);
        //    var ftpStatus = await ftpClient.UploadStream(
        //        fileStream,
        //        remoteFilePath,
        //        ftpRemoteExists,
        //        true,
        //        _progress,
        //        cancellationToken);
        //    return ftpStatus;
        //}

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

            switch (FtpUploadConfig.FileExistsTimeUnit)
            {
                case FileTimeUnit.Seconds:
                    timeSpan = TimeSpan.FromSeconds(FtpUploadConfig.FileExistsTime);
                    break;
                case FileTimeUnit.Minutes:
                    timeSpan = TimeSpan.FromMinutes(FtpUploadConfig.FileExistsTime);
                    break;
                case FileTimeUnit.Hours:
                    timeSpan = TimeSpan.FromHours(FtpUploadConfig.FileExistsTime);
                    break;
                case FileTimeUnit.Days:
                    timeSpan = TimeSpan.FromDays(FtpUploadConfig.FileExistsTime);
                    break;
                default:
                    break;
            }
            switch (FtpUploadConfig.FileExistsTimeRange)
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
