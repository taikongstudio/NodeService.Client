using FluentFTP;
using Microsoft.Extensions.Logging;
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.ServiceHost.Tasks
{
    public class FtpDownloadConfigExecutor
    {
        int skippedCount = 0;
        int downloadedCount = 0;
        int overidedCount;

        public FtpDownloadConfigModel FtpDownloadConfig { get; private set; }

        private readonly IProgress<FtpProgress> _progress;

        public readonly ILogger _logger;

        public FtpDownloadConfigExecutor(
            IProgress<FtpProgress> progress,
            FtpDownloadConfigModel ftpDownloadConfig,
            ILogger logger)
        {
            _logger = logger;
            _progress = progress;
            FtpDownloadConfig = ftpDownloadConfig;

        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            using var ftpClient = new AsyncFtpClient(FtpDownloadConfig.FtpConfig.Host,
                FtpDownloadConfig.FtpConfig.Username,
                FtpDownloadConfig.FtpConfig.Password,
                FtpDownloadConfig.FtpConfig.Port, new FtpConfig()
                {
                    ConnectTimeout = FtpDownloadConfig.FtpConfig.ConnectTimeout,
                    ReadTimeout = FtpDownloadConfig.FtpConfig.ReadTimeout,
                    DataConnectionReadTimeout = FtpDownloadConfig.FtpConfig.DataConnectionReadTimeout,
                    DataConnectionConnectTimeout = FtpDownloadConfig.FtpConfig.DataConnectionConnectTimeout,
                });
            await ftpClient.AutoConnect(cancellationToken);
            _logger.LogInformation($"host {FtpDownloadConfig.FtpConfig.Host} port {FtpDownloadConfig.FtpConfig.Port}" +
                $" username {FtpDownloadConfig.FtpConfig.Username} password {FtpDownloadConfig.FtpConfig.Password}");

            var hostName = Dns.GetHostName();

            string localRootPath = FtpDownloadConfig.LocalDirectory;

            if (!Directory.Exists(localRootPath))
            {
                Directory.CreateDirectory(localRootPath);
            }

            if (FtpDownloadConfig.CleanupLocalDirectory)
            {
                CleanupInstallDirectory(localRootPath);
            }

            var ftpListOption = FtpDownloadConfig.IncludeSubDirectories ? FtpListOption.Recursive : FtpListOption.Auto;

            var remoteFileListDict = (await ftpClient.GetListing(FtpDownloadConfig.RemoteDirectory, ftpListOption, cancellationToken)).Where(x => x.Type == FtpObjectType.File).ToDictionary<FtpListItem, string>(x => x.FullName);

            PrintLocalFiles(localRootPath);

            PrintRemoteFiles(remoteFileListDict);

            foreach (var item in remoteFileListDict)
            {
                var localFilePath = Path.Combine(localRootPath, Path.GetRelativePath(FtpDownloadConfig.RemoteDirectory, item.Key));
                localFilePath = Path.GetFullPath(localFilePath);
                var remoteFileInfo = item.Value;
                var remoteFilePath = remoteFileInfo.FullName;
                await DownloadFileAsync(ftpClient, remoteFileInfo, localFilePath, cancellationToken);
            }

            _logger.LogInformation($"downloadedCount:{downloadedCount} skippedCount:{skippedCount} overidedCount:{overidedCount}");
        }

        private void PrintLocalFiles(string localRootPath)
        {
            if (!FtpDownloadConfig.PrintLocalFiles)
            {
                return;
            }

            var localFiles = Directory.GetFiles(localRootPath, FtpDownloadConfig.SearchPattern, new EnumerationOptions()
            {
                MatchCasing = FtpDownloadConfig.MatchCasing,
                RecurseSubdirectories = FtpDownloadConfig.IncludeSubDirectories,
            });

            _logger.LogInformation("Enumerate local Objects Begin");
            foreach (var localFile in localFiles)
            {
                var fileInfo = new FileInfo(localFile);
                _logger.LogInformation($"FullName:{fileInfo.FullName},ModifiedTime:{fileInfo.LastWriteTime},Length:{fileInfo.Length}");
            }
            _logger.LogInformation("Enumerate local Objects End");
        }

        private void PrintRemoteFiles(Dictionary<string, FtpListItem> remoteFileListDict)
        {
            if (!FtpDownloadConfig.PrintRemoteFiles)
            {
                return;
            }
            _logger.LogInformation("Enumerate Ftp objects Begin");
            foreach (var item in remoteFileListDict)
            {
                _logger.LogInformation($"{item}");
            }
            _logger.LogInformation("Enumerate Ftp objects End");
        }

        private async Task DownloadFileAsync(AsyncFtpClient ftpClient, FtpListItem remoteFileInfo, string localFilePath, CancellationToken cancellationToken)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;
            int retryTimes = 0;
            do
            {
                ftpStatus = await DownloadFileCoreAsync(ftpClient, remoteFileInfo, localFilePath, cancellationToken);
                retryTimes++;
                if (ftpStatus == FtpStatus.Failed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            } while (
            !cancellationToken.IsCancellationRequested
            &&
            ftpStatus == FtpStatus.Failed
            &&
            (FtpDownloadConfig.RetryTimes == 0 ||

            FtpDownloadConfig.RetryTimes > 0
            &&
            retryTimes <= FtpDownloadConfig.RetryTimes
            ));

            if (ftpStatus == FtpStatus.Success)
            {
                if (remoteFileInfo.Modified != DateTime.MinValue)
                {
                    File.SetLastWriteTime(localFilePath, remoteFileInfo.Modified);
                }
                if (remoteFileInfo.Created != DateTime.MinValue)
                {
                    File.SetCreationTime(localFilePath, remoteFileInfo.Created);
                }

                _logger.LogInformation($"Download:LocalPath:{localFilePath} RemotePath:{remoteFileInfo.FullName}");
                downloadedCount++;
            }
            else if (ftpStatus == FtpStatus.Skipped)
            {
                skippedCount++;
                _logger.LogInformation($"Skip:LocalPath:{localFilePath},RemotePath:{remoteFileInfo.FullName}");
            }
        }

        private async Task<FtpStatus> DownloadFileCoreAsync(AsyncFtpClient ftpClient, FtpListItem remoteFileInfo, string localFilePath, CancellationToken cancellationToken = default)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;
            try
            {
                if (!await ftpClient.FileExists(remoteFileInfo.FullName, cancellationToken))
                {
                    return FtpStatus.Skipped;
                }
                if (!FtpDownloadConfig.CleanupLocalDirectory
                    && FtpDownloadConfig.FileExistsTime > 0
                    && DiffFileInfo(localFilePath, remoteFileInfo))
                {
                    var fileExists = ConvertFtpFileExistsToFtpLocalExists(FtpDownloadConfig.FtpFileExists);
                    ftpStatus = await ftpClient.DownloadFile(
                         localFilePath,
                         remoteFileInfo.FullName,
                         fileExists,
                         progress: _progress,
                         token: cancellationToken);
                    if (ftpStatus == FtpStatus.Success)
                    {
                        if (fileExists == FtpLocalExists.Overwrite)
                        {
                            overidedCount++;
                        }
                    }
                }
                else
                {
                    ftpStatus = await ftpClient.DownloadFile(localFilePath,
                         remoteFileInfo.FullName,
                         FtpLocalExists.Skip,
                         progress: _progress,
                         token: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                ftpStatus = FtpStatus.Failed;
            }


            return ftpStatus;
        }

        private FtpLocalExists ConvertFtpFileExistsToFtpLocalExists(FtpFileExists ftpFileExists)
        {
            switch (ftpFileExists)
            {
                case FtpFileExists.Skip:
                    return FtpLocalExists.Skip;
                case FtpFileExists.Overwrite:
                    return FtpLocalExists.Overwrite;
                case FtpFileExists.Resume:
                    return FtpLocalExists.Resume;
                default:
                    break;
            }
            return FtpLocalExists.Overwrite;
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
            _logger.LogInformation($"CompareFileInfo:Local:{localFileInfo.FullName}=>Remote:{remoteFileInfo.FullName}" +
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

            switch (FtpDownloadConfig.FileExistsTimeUnit)
            {
                case FileExistsTimeUnit.Seconds:
                    timeSpan = TimeSpan.FromSeconds(FtpDownloadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Minutes:
                    timeSpan = TimeSpan.FromMinutes(FtpDownloadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Hours:
                    timeSpan = TimeSpan.FromHours(FtpDownloadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Days:
                    timeSpan = TimeSpan.FromDays(FtpDownloadConfig.FileExistsTime);
                    break;
                default:
                    break;
            }
            switch (FtpDownloadConfig.FileExistsTimeRange)
            {
                case FileExistsTimeRange.WithinRange:
                    compareDateTime = Math.Abs((remoteFileInfo.Modified - lastWriteTime).TotalSeconds) <= timeSpan.TotalSeconds;
                    break;
                case FileExistsTimeRange.OutOfRange:
                    compareDateTime = Math.Abs((remoteFileInfo.Modified - lastWriteTime).TotalSeconds) > timeSpan.TotalSeconds;
                    break;
                default:
                    break;
            }
            return compareDateTime;
        }

        private void CleanupInstallDirectory(string path)
        {
            var directoryInfo = new DirectoryInfo(path);
            foreach (var item in directoryInfo.GetFileSystemInfos())
            {
                if (Directory.Exists(item.FullName))
                {
                    Directory.Delete(item.FullName, true);
                }
                else
                {
                    item.Delete();
                }
            }

        }

    }
}
