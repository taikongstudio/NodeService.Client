using FluentFTP;
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class FtpDownloadConfigExecutor
    {
        int skippedCount = 0;
        int successCount = 0;

        public FtpDownloadConfigModel FtpDownloadConfig { get; set; }

        private readonly MyFtpProgress _myFtpProgress;

        public ILogger Logger { get; set; }

        public FtpDownloadConfigExecutor(
            MyFtpProgress myFtpProgress,
            FtpDownloadConfigModel ftpDownloadConfig,
            ILogger logger)
        {
            _myFtpProgress = myFtpProgress;
            FtpDownloadConfig = ftpDownloadConfig;
            Logger = logger;
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
            Logger.LogInformation($"Execute config on host {FtpDownloadConfig.FtpConfig.Host} port {FtpDownloadConfig.FtpConfig.Port}" +
                $" username {FtpDownloadConfig.FtpConfig.Username} password {FtpDownloadConfig.FtpConfig.Password}");

            var hostName = Dns.GetHostName();

            string localRootPath = FtpDownloadConfig.LocalDirectory;

            if (!Directory.Exists(localRootPath))
            {
                Directory.CreateDirectory(localRootPath);
            }

            if (this.FtpDownloadConfig.CleanupLocalDirectory)
            {
                CleanupInstallDirectory(localRootPath);
            }

            Logger.LogInformation("Enumerate local Objects Begin");


            var ftpListOption = FtpDownloadConfig.IncludeSubDirectories ? FtpListOption.Recursive : FtpListOption.Auto;

            var remoteFileListDict = (await ftpClient.GetListing(FtpDownloadConfig.RemoteDirectory, ftpListOption, cancellationToken)).Where(x => x.Type == FtpObjectType.File).ToDictionary<FtpListItem, string>(x => x.FullName);

            Logger.LogInformation("Enumerate Ftp objects Begin");
            foreach (var item in remoteFileListDict)
            {
                Logger.LogInformation($"FullName:{item.Key},ModifiedTime:{item.Value.Modified},Length:{item.Value.Size}");
            }
            Logger.LogInformation("Enumerate Ftp objects End");

            foreach (var item in remoteFileListDict)
            {
                var localFilePath = Path.Combine(localRootPath, Path.GetRelativePath(FtpDownloadConfig.RemoteDirectory, item.Key));
                localFilePath = Path.GetFullPath(localFilePath);
                FtpStatus ftpStatus = FtpStatus.Failed;
                int retryTimes = 0;
                do
                {
                    ftpStatus = await DownloadFileAsync(ftpClient, item.Value, localFilePath, cancellationToken);
                    retryTimes++;
                    if (ftpStatus == FtpStatus.Failed)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    }
                } while (
                !cancellationToken.IsCancellationRequested
                &&
                ftpStatus == FtpStatus.Failed
                &&
                (this.FtpDownloadConfig.RetryTimes == 0 ||

                (this.FtpDownloadConfig.RetryTimes > 0
                &&
                retryTimes <= this.FtpDownloadConfig.RetryTimes
                )));

            }

            Logger.LogInformation($"successCount:{successCount} skippedCount:{skippedCount}");
        }

        private async Task<FtpStatus> DownloadFileAsync(AsyncFtpClient ftpClient, FtpListItem remoteFileInfo, string localFilePath, CancellationToken cancellationToken = default)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;
            try
            {
                if (!await ftpClient.FileExists(remoteFileInfo.FullName, cancellationToken))
                {
                    return FtpStatus.Skipped;
                }
                if (!this.FtpDownloadConfig.CleanupLocalDirectory && this.FtpDownloadConfig.FileExistsTime > 0 && CompareFileInfo(localFilePath, remoteFileInfo))
                {
                    ftpStatus = await ftpClient.DownloadFile(localFilePath,
                         remoteFileInfo.FullName,
                         (FtpLocalExists)this.FtpDownloadConfig.FtpFileExists,
                         progress: _myFtpProgress,
                         token: cancellationToken);
                }
                else
                {
                    ftpStatus = await ftpClient.DownloadFile(localFilePath,
                         remoteFileInfo.FullName,
                         FtpLocalExists.Skip,
                         progress: _myFtpProgress,
                         token: cancellationToken);
                }
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

                    Logger.LogInformation($"FullName:{remoteFileInfo} CreationTime:{File.GetCreationTime(localFilePath)} LastWriteTime:{File.GetLastWriteTime(localFilePath)}  success");
                    successCount++;
                }
                else if (ftpStatus == FtpStatus.Skipped)
                {
                    Logger.LogInformation($"FullName:{remoteFileInfo}  skipped");
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                ftpStatus = FtpStatus.Failed;
            }


            return ftpStatus;
        }

        private bool CompareFileInfo(string localFilePath, FtpListItem remoteFileInfo)
        {
            if (!File.Exists(localFilePath))
            {
                return false;
            }
            bool compareTime = true;
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

            var lastWriteTime = File.GetLastWriteTime(localFilePath);
            switch (FtpDownloadConfig.FileExistsTimeRange)
            {
                case FileExistsTimeRange.WithinRange:
                    compareTime = (remoteFileInfo.Modified - lastWriteTime) <= timeSpan;
                    break;
                case FileExistsTimeRange.OutOfRange:
                    compareTime = (remoteFileInfo.Modified - lastWriteTime) > timeSpan;
                    break;
                default:
                    break;
            }
            return compareTime;
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
