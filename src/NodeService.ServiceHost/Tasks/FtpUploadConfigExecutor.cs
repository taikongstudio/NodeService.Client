using FluentFTP;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Concurrent;

namespace NodeService.ServiceHost.Tasks
{
    public class FtpUploadConfigExecutor
    {
        int _uploadedCount = 0;
        int _skippedCount = 0;
        int _overidedCount = 0;

        ConcurrentDictionary<string, FtpListItem> _remoteFileListDict;

        readonly IProgress<FtpProgress> _progress;

        public FtpUploadConfigModel FtpUploadConfig { get; private set; }

        public ILogger _logger { get; set; }


        ImmutableDictionary<string, string?> _envVars;

        string? _fileSystemWatchPath;
        string? _fileSystemWatchRelativePath;
        string? _fileSystemWatchEventLocalDirectory;

        public FtpUploadConfigExecutor(
            IProgress<FtpProgress> progress,
            FtpUploadConfigModel ftpTaskConfig,
            ILogger logger)
        {
            _progress = progress;
            FtpUploadConfig = ftpTaskConfig;
            _logger = logger;
            _remoteFileListDict = new ConcurrentDictionary<string, FtpListItem>();
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
        }

        private string GetLocalDirectory()
        {
            if (_fileSystemWatchEventLocalDirectory != null)
            {
                return _fileSystemWatchEventLocalDirectory;
            }
            return FtpUploadConfig.LocalDirectory;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            using var ftpClient = new AsyncFtpClient(FtpUploadConfig.FtpConfig.Host,
                FtpUploadConfig.FtpConfig.Username,
                FtpUploadConfig.FtpConfig.Password,
                FtpUploadConfig.FtpConfig.Port, new FtpConfig()
                {
                    ConnectTimeout = FtpUploadConfig.FtpConfig.ConnectTimeout,
                    ReadTimeout = FtpUploadConfig.FtpConfig.ReadTimeout,
                    DataConnectionReadTimeout = FtpUploadConfig.FtpConfig.DataConnectionReadTimeout,
                    DataConnectionConnectTimeout = FtpUploadConfig.FtpConfig.DataConnectionConnectTimeout,
                });
            await ftpClient.AutoConnect(cancellationToken);
            _logger.LogInformation($"Execute config on host {FtpUploadConfig.FtpConfig.Host} port {FtpUploadConfig.FtpConfig.Port}" +
                $" username {FtpUploadConfig.FtpConfig.Username} password {FtpUploadConfig.FtpConfig.Password}");

            var hostName = Dns.GetHostName();


            string rootPath = GetLocalDirectory();

            if (!Directory.Exists(rootPath))
            {
                _logger.LogInformation($"Could not found directory:{rootPath}");
                return;
            }

            if (FtpUploadConfig.Filters == null)
            {
                FtpUploadConfig.Filters = [];
            }
            if (!FtpUploadConfig.Filters.Any(IsSearchPatternFilter))
            {
                FtpUploadConfig.Filters.Add(new StringEntry()
                {
                    Name = nameof(FilePathFilter.SearchPattern),
                    Value = FtpUploadConfig.SearchPattern,
                });
            }

            IEnumerable<string> localFilePathList = EnumerateLocalFiles();

            if (!localFilePathList.Any())
            {
                Console.WriteLine($"Cound not found any mached file in {rootPath}");
                return;
            }

            PrintLocalFiles(localFilePathList);

            FtpUploadConfig.RemoteDirectory = FtpUploadConfig.RemoteDirectory.Replace("$(HostName)", hostName).Replace("\\", "/");

            if (!FtpUploadConfig.RemoteDirectory.StartsWith('/'))
            {
                FtpUploadConfig.RemoteDirectory = '/' + FtpUploadConfig.RemoteDirectory;
            }

            var remoteFilePathList = localFilePathList.GroupBy(GetPathDirectoryName).SelectMany(CalculateDirectoryRemoteFilePath).ToImmutableArray();
            foreach (var kv in remoteFilePathList)
            {
                var ftpListItem = await ftpClient.GetObjectInfo(
                    kv.Value,
                    true,
                    cancellationToken);
                this._remoteFileListDict.TryAdd(kv.Value, ftpListItem);
            }

            PrintRemoteFiles();

            foreach (var kv in remoteFilePathList)
            {
                await UploadFileAsync(
                    ftpClient,
                    kv.Key,
                    kv.Value,
                    cancellationToken);
            }

            _logger.LogInformation($"uploadedCount:{_uploadedCount} skippedCount:{_skippedCount} overidedCount:{_overidedCount}");

        }

        private IEnumerable<string> EnumerateLocalFiles()
        {
            IEnumerable<string> localFiles = EnumerateFiles(GetLocalDirectory(), "*");
            foreach (var filter in FtpUploadConfig.Filters)
            {
                if (filter.Name == null || string.IsNullOrEmpty(filter.Value))
                {
                    continue;
                }

                if (!Enum.TryParse(filter.Name, true, out FilePathFilter pathFilter))
                {
                    continue;
                }
                switch (pathFilter)
                {
                    case FilePathFilter.Contains:
                        localFiles = from item in localFiles
                                     where item.Contains(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.NotContains:
                        localFiles = from item in localFiles
                                     where !item.Contains(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.StartWith:
                        localFiles = from item in localFiles
                                     where item.StartsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.NotStartWith:
                        localFiles = from item in localFiles
                                     where !item.StartsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.EndsWith:
                        localFiles = from item in localFiles
                                     where item.EndsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.NotEndsWith:
                        localFiles = from item in localFiles
                                     where !item.EndsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.RegExp:
                        {
                            var regex = new Regex(filter.Value);
                            localFiles = from item in localFiles
                                         where regex.IsMatch(item)
                                         select item;
                        }

                        break;
                    case FilePathFilter.RegExpNotMatch:
                        {
                            var regex = new Regex(filter.Value);
                            localFiles = from item in localFiles
                                         where !regex.IsMatch(item)
                                         select item;
                        }
                        break;
                    case FilePathFilter.SearchPattern:
                        localFiles = localFiles.GroupBy(GetPathDirectoryName)
                                               .SelectMany(x => EnumerateFiles(x.Key, filter.Value)).Distinct();
                        break;
                    default:
                        break;
                }
            }

            if (FtpUploadConfig.DateTimeFilters != null && FtpUploadConfig.DateTimeFilters.Count != 0)
            {
                localFiles = localFiles.Where(DateTimeFilter);
            }

            if (FtpUploadConfig.LengthFilters != null && FtpUploadConfig.LengthFilters.Count != 0)
            {
                localFiles = localFiles.Where(FileLengthFilter);
            }

            return localFiles.ToImmutableArray();
        }

        private string[] EnumerateFiles(
            string path,
            string? searchPattern)
        {
            if (string.IsNullOrEmpty(searchPattern))
            {
                return [];
            }
            return Directory.GetFiles(path,
                searchPattern,
                new EnumerationOptions()
                {
                    MatchCasing = FtpUploadConfig.MatchCasing,
                    RecurseSubdirectories = FtpUploadConfig.IncludeSubDirectories,
                    MaxRecursionDepth = FtpUploadConfig.MaxRecursionDepth,
                    MatchType = FtpUploadConfig.MatchType,
                }
                );
        }

        private bool FileLengthFilter(string path)
        {
            var fileInfo = new FileInfo(path);
            var matchedCount = 0;
            foreach (var fileLengthFilter in FtpUploadConfig.LengthFilters)
            {
                var value0 = CalcuateLength(fileLengthFilter.LengthUnit, fileLengthFilter.Values[0]);
                var value1 = CalcuateLength(fileLengthFilter.LengthUnit, fileLengthFilter.Values[1]);
                switch (fileLengthFilter.Operator)
                {
                    case CompareOperator.LessThan:
                        if (fileInfo.Length < value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.GreatThan:
                        if (fileInfo.Length > value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.LessThanEqual:
                        if (fileInfo.Length <= value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.GreatThanEqual:
                        if (fileInfo.Length >= value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.Equals:
                        if (fileInfo.Length == value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.WithinRange:
                        if (fileInfo.Length >= value0 && fileInfo.Length <= value1)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.OutOfRange:
                        if (!(fileInfo.Length >= value0 && fileInfo.Length <= value1))
                        {
                            matchedCount++;
                        }
                        break;
                    default:
                        break;
                }
            }
            return matchedCount > 0;

            static long CalcuateLength(BinaryLengthUnit binaryLengthUnit, double value)
            {
                var length = binaryLengthUnit switch
                {
                    BinaryLengthUnit.Byte => value,
                    BinaryLengthUnit.KB => value * 1024,
                    BinaryLengthUnit.MB => value * 1024 * 1024,
                    BinaryLengthUnit.GB => value * 1024 * 1024 * 1024,
                    BinaryLengthUnit.PB => value * 1024 * 1024 * 1024 * 1024,
                    _ => throw new NotImplementedException(),
                };
                return (long)length;
            }
        }

        private bool DateTimeFilter(string path)
        {
            bool isMatched = false;
            var lastWriteTime = File.GetLastWriteTime(path);
            var creationTime = File.GetCreationTime(path);
            foreach (var dateTimeFilter in FtpUploadConfig.DateTimeFilters)
            {
                switch (dateTimeFilter.Kind)
                {
                    case DateTimeFilterKind.DateTime:
                        isMatched = dateTimeFilter.IsMatched(lastWriteTime);
                        break;
                    case DateTimeFilterKind.TimeOnly:
                        isMatched = dateTimeFilter.IsMatched(TimeOnly.FromDateTime(lastWriteTime));
                        break;
                    case DateTimeFilterKind.Days:
                    case DateTimeFilterKind.Hours:
                    case DateTimeFilterKind.Minutes:
                    case DateTimeFilterKind.Seconds:
                        isMatched = dateTimeFilter.IsMatched(DateTime.Now - lastWriteTime);
                        break;
                    default:
                        break;
                }

                if (isMatched)
                {
                    break;
                }
            }
            return isMatched;
        }

        private bool IsSearchPatternFilter(StringEntry entry)
        {
            return Enum.TryParse(
                entry.Name,
                true,
                out FilePathFilter pathFilter) && pathFilter == Infrastructure.DataModels.FilePathFilter.SearchPattern;
        }

        private async Task UploadFileAsync(
            AsyncFtpClient ftpClient,
            string localFilePath,
            string remoteFilePath,
            CancellationToken cancellationToken)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;
            int retryTimes = 0;
            do
            {
                ftpStatus = await UploadFileCoreAsync(
                    ftpClient,
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
                var lastWriteTime = File.GetLastWriteTime(localFilePath);
                await ftpClient.SetModifiedTime(remoteFilePath, lastWriteTime, cancellationToken);
                _logger.LogInformation($"{remoteFilePath}:SetModifiedTime:{lastWriteTime}");
            }
            else if (ftpStatus == FtpStatus.Skipped)
            {
                _skippedCount++;
                _logger.LogInformation($"Skip:LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
            }
        }

        private void PrintRemoteFiles()
        {
            if (!FtpUploadConfig.PrintRemoteFiles)
            {
                return;
            }
            _logger.LogInformation("Enumerate Ftp objects Begin");
            foreach (var item in _remoteFileListDict)
            {
                _logger.LogInformation($"FullName:{item.Key},ModifiedTime:{item.Value.Modified},Length:{item.Value.Size}");
            }
            _logger.LogInformation("Enumerate Ftp objects End");
        }

        private void PrintLocalFiles(IEnumerable<string> localFiles)
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

        private async Task<FtpStatus> UploadFileCoreAsync(
            AsyncFtpClient ftpClient,
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
                if (uploadSharingViolationFile)
                {
                    ftpStatus = await UploadSharingViolationFileAsync(ftpClient,
                        localFilePath,
                        remoteFilePath,
                        ftpRemoteExists,
                        cancellationToken);
                    return ftpStatus;
                }

                if (FtpUploadConfig.CleanupRemoteDirectory
                    || FtpUploadConfig.FileExistsTime <= 0
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
                ftpStatus = await ftpClient.UploadFile(localFilePath,
                     remoteFilePath,
                     ftpRemoteExists,
                     true,
                     FtpVerify.None,
                     _progress, token: cancellationToken);
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

        private async Task<FtpStatus> UploadSharingViolationFileAsync(
            AsyncFtpClient ftpClient,
            string localFilePath,
            string remoteFilePath,
            FtpRemoteExists ftpRemoteExists,
            CancellationToken cancellationToken = default)
        {
            using var fileStream = File.Open(
                localFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            var ftpStatus = await ftpClient.UploadStream(
                fileStream,
                remoteFilePath,
                ftpRemoteExists,
                true,
                _progress,
                cancellationToken);
            return ftpStatus;
        }

        private FtpRemoteExists ConvertFtpFileExistsToFtpRemoteExists(FileExists ftpFileExists)
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

        private bool DiffFileInfo(string localFilePath, FtpListItem remoteFileInfo)
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
                $"{nameof(DiffSize)}:{diffSize} {nameof(DiffFileTime)}:{diffFileTime}");
            return diffSize && diffFileTime;
        }

        private long DiffSize(FileInfo localFileInfo, FtpListItem remoteFileInfo)
        {
            return localFileInfo.Length - remoteFileInfo.Size;
        }

        private bool DiffFileTime(FileInfo localFileInfo, FtpListItem remoteFileInfo)
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
                    compareDateTime = Math.Abs((int)(remoteFileInfo.Modified - lastWriteTime).TotalSeconds) < (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.GreatThan:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.Modified - lastWriteTime).TotalSeconds) > (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.LessThanEqual:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.Modified - lastWriteTime).TotalSeconds) <= (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.GreatThanEqual:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.Modified - lastWriteTime).TotalSeconds) >= (int)timeSpan.TotalSeconds;
                    break;
                case CompareOperator.Equals:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.Modified - lastWriteTime).TotalSeconds) == (int)timeSpan.TotalSeconds;
                    break;
                default:
                    break;
            }
            return compareDateTime;
        }

        IEnumerable<KeyValuePair<string, string>> CalculateDirectoryRemoteFilePath(IGrouping<string?, string> filePathDirectoryGroup)
        {
            if (filePathDirectoryGroup == null || filePathDirectoryGroup.Key == null)
            {
                yield break;
            }
            var relativePath = Path.GetRelativePath(FtpUploadConfig.LocalDirectory, filePathDirectoryGroup.Key);
            foreach (var localFilePath in filePathDirectoryGroup)
            {
                var fileName = Path.GetFileName(localFilePath);
                var remoteFilePath = Path.Combine(FtpUploadConfig.RemoteDirectory, relativePath, fileName);
                remoteFilePath = remoteFilePath.Replace("\\", "/");
                remoteFilePath = remoteFilePath.Replace("/./", "/");
                yield return KeyValuePair.Create(localFilePath, remoteFilePath);
            }
            yield break;
        }

        string GetPathDirectoryName(string path)
        {
            return Path.GetDirectoryName(path) ?? path;
        }

    }
}
