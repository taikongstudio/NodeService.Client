using FluentFTP;
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class FtpUploadConfigExecutor
    {
        int uploadedCount = 0;
        int skippedCount = 0;
        int overidedCount = 0;

        private Dictionary<string, FtpListItem> _remoteFileListDict;

        public FtpUploadConfigModel FtpUploadConfig { get; private set; }

        private readonly IProgress<FtpProgress> _progress;

        public ILogger _logger { get; set; }

        public FtpUploadConfigExecutor(
            IProgress<FtpProgress> progress,
            FtpUploadConfigModel ftpTaskConfig,
            ILogger logger)
        {
            _progress = progress;
            FtpUploadConfig = ftpTaskConfig;
            _logger = logger;
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


            string rootPath = FtpUploadConfig.LocalDirectory;

            if (!Directory.Exists(rootPath))
            {
                _logger.LogInformation($"Could not found directory:{rootPath}");
                return;
            }

            var localFiles = Directory.GetFiles(rootPath, FtpUploadConfig.SearchPattern, new EnumerationOptions()
            {
                MatchCasing = FtpUploadConfig.MatchCasing,
                RecurseSubdirectories = FtpUploadConfig.IncludeSubDirectories,
            });
            if (localFiles.Length == 0)
            {
                Console.WriteLine($"Cound not found any files in {rootPath}");
                return;
            }

            PrintLocalFiles(localFiles);

            if (FtpUploadConfig.Filters != null && FtpUploadConfig.Filters.Any())
            {
                localFiles = localFiles.Where(FilterPath).ToArray();
            }


            FtpUploadConfig.RemoteDirectory = FtpUploadConfig.RemoteDirectory.Replace("$(HostName)", hostName).Replace("\\", "/");

            if (!FtpUploadConfig.RemoteDirectory.StartsWith('/'))
            {
                FtpUploadConfig.RemoteDirectory = '/' + FtpUploadConfig.RemoteDirectory;
            }

            _remoteFileListDict = (await ftpClient.GetListing(FtpUploadConfig.RemoteDirectory,
                FtpUploadConfig.IncludeSubDirectories ? FtpListOption.Recursive : FtpListOption.Auto
                , cancellationToken)).ToDictionary<FtpListItem, string>(static x => x.FullName);

            PrintRemoteFiles();

            foreach (var directoryFileGroup in localFiles.GroupBy<string, string>(Path.GetDirectoryName))
            {
                foreach (var localFilePath in directoryFileGroup)
                {
                    string remoteFilePath = CaculateRemoteFilePath(FtpUploadConfig.RemoteDirectory, rootPath, localFilePath);

                    await UploadFileAsync(ftpClient, localFilePath, remoteFilePath, cancellationToken);
                }
            }
            _logger.LogInformation($"uploadedCount:{uploadedCount} skippedCount:{skippedCount} overidedCount:{overidedCount}");

        }

        private bool FilterPath(string path)
        {
            foreach (var filter in FtpUploadConfig.Filters)
            {
                if (filter.Value == null)
                {
                    continue;
                }
                if (path.Contains(filter.Value))
                {
                    return true;
                }
            }
            return false;
        }

        private async Task UploadFileAsync(AsyncFtpClient ftpClient, string localFilePath, string remoteFilePath, CancellationToken cancellationToken)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;
            int retryTimes = 0;
            do
            {
                ftpStatus = await UploadFileCoreAsync(ftpClient, localFilePath, remoteFilePath, cancellationToken);
                retryTimes++;
                if (ftpStatus == FtpStatus.Failed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }

            } while (!cancellationToken.IsCancellationRequested
                &&
                ftpStatus == FtpStatus.Failed
                &&
                (this.FtpUploadConfig.RetryTimes == 0 ||

                (this.FtpUploadConfig.RetryTimes > 0
                &&
                retryTimes <= this.FtpUploadConfig.RetryTimes
                )));

            if (ftpStatus == FtpStatus.Success)
            {
                uploadedCount++;
                _logger.LogInformation($"Upload:LocalPath:{localFilePath} RemotePath:{remoteFilePath}");
                var lastWriteTime = File.GetLastWriteTime(localFilePath);
                await ftpClient.SetModifiedTime(remoteFilePath, lastWriteTime, cancellationToken);
                _logger.LogInformation($"{remoteFilePath}:SetModifiedTime:{lastWriteTime}");
            }
            else if (ftpStatus == FtpStatus.Skipped)
            {
                skippedCount++;
                _logger.LogInformation($"Skip:LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
            }
        }

        private void PrintRemoteFiles()
        {
            if (!this.FtpUploadConfig.PrintRemoteFiles)
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

        private void PrintLocalFiles(string[] localFiles)
        {
            if (!this.FtpUploadConfig.PrintLocalFiles)
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
                FtpRemoteExists ftpRemoteExists = ConvertFtpFileExistsToFtpRemoteExists(this.FtpUploadConfig.FtpFileExists);
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
                    || this.FtpUploadConfig.FileExistsTime <= 0
                    || !_remoteFileListDict.TryGetValue(remoteFilePath, out var ftpListItem)
                    || !DiffFileInfo(localFilePath, ftpListItem))
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
                        overidedCount++;
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
                this._progress,
                cancellationToken);
            return ftpStatus;
        }

        private FtpRemoteExists ConvertFtpFileExistsToFtpRemoteExists(FtpFileExists ftpFileExists)
        {
            switch (ftpFileExists)
            {
                case FtpFileExists.NoCheck:
                    return FtpRemoteExists.NoCheck;
                case FtpFileExists.ResumeNoCheck:
                    return FtpRemoteExists.ResumeNoCheck;
                case FtpFileExists.AddToEndNoCheck:
                    return FtpRemoteExists.AddToEndNoCheck;
                case FtpFileExists.Skip:
                    return FtpRemoteExists.Skip;
                case FtpFileExists.Overwrite:
                    return FtpRemoteExists.Overwrite;
                case FtpFileExists.Resume:
                    return FtpRemoteExists.Resume;
                case FtpFileExists.AddToEnd:
                    return FtpRemoteExists.AddToEnd;
                default:
                    break;
            }
            return FtpRemoteExists.Skip;
        }

        private bool DiffFileInfo(string localFilePath, FtpListItem remoteFileInfo)
        {
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

            switch (this.FtpUploadConfig.FileExistsTimeUnit)
            {
                case FileExistsTimeUnit.Seconds:
                    timeSpan = TimeSpan.FromSeconds(this.FtpUploadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Minutes:
                    timeSpan = TimeSpan.FromMinutes(this.FtpUploadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Hours:
                    timeSpan = TimeSpan.FromHours(this.FtpUploadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Days:
                    timeSpan = TimeSpan.FromDays(this.FtpUploadConfig.FileExistsTime);
                    break;
                default:
                    break;
            }
            switch (FtpUploadConfig.FileExistsTimeRange)
            {
                case FileExistsTimeRange.WithinRange:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.Modified - lastWriteTime).TotalSeconds) <= (int)timeSpan.TotalSeconds;
                    break;
                case FileExistsTimeRange.OutOfRange:
                    compareDateTime = Math.Abs((int)(remoteFileInfo.Modified - lastWriteTime).TotalSeconds) > (int)timeSpan.TotalSeconds;
                    break;
                default:
                    break;
            }
            return compareDateTime;
        }

        private static string CaculateRemoteFilePath(string remoteRootDir, string rootPath, string? localFilePath)
        {
            var parentDirectory = Path.GetDirectoryName(localFilePath);
            var relativePath = Path.GetRelativePath(rootPath, parentDirectory);
            var fileName = Path.GetFileName(localFilePath);
            var remoteFilePath = Path.Combine(remoteRootDir, relativePath, fileName);
            remoteFilePath = remoteFilePath.Replace("\\", "/");
            remoteFilePath = remoteFilePath.Replace("/./", "/");
            return remoteFilePath;
        }

    }
}
