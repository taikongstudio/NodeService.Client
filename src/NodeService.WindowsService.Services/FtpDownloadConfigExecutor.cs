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

        public readonly FtpDownloadConfigModel _ftpDownloadConfig;

        private readonly MyFtpProgress _myFtpProgress;

        public readonly ILogger _logger;

        private readonly EventId _eventId;

        public FtpDownloadConfigExecutor(
            EventId eventId,
            MyFtpProgress myFtpProgress,
            FtpDownloadConfigModel ftpDownloadConfig,
            ILogger logger)
        {
            _myFtpProgress = myFtpProgress;
            _ftpDownloadConfig = ftpDownloadConfig;
            _logger = logger;
            _eventId = eventId;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            using var ftpClient = new AsyncFtpClient(_ftpDownloadConfig.FtpConfig.Host,
                _ftpDownloadConfig.FtpConfig.Username,
                _ftpDownloadConfig.FtpConfig.Password,
                _ftpDownloadConfig.FtpConfig.Port, new FtpConfig()
                {
                    ConnectTimeout = _ftpDownloadConfig.FtpConfig.ConnectTimeout,
                    ReadTimeout = _ftpDownloadConfig.FtpConfig.ReadTimeout,
                    DataConnectionReadTimeout = _ftpDownloadConfig.FtpConfig.DataConnectionReadTimeout,
                    DataConnectionConnectTimeout = _ftpDownloadConfig.FtpConfig.DataConnectionConnectTimeout,
                });
            await ftpClient.AutoConnect(cancellationToken);
            _logger.LogInformation( $"host {_ftpDownloadConfig.FtpConfig.Host} port {_ftpDownloadConfig.FtpConfig.Port}" +
                $" username {_ftpDownloadConfig.FtpConfig.Username} password {_ftpDownloadConfig.FtpConfig.Password}");

            var hostName = Dns.GetHostName();

            string localRootPath = _ftpDownloadConfig.LocalDirectory;

            if (!Directory.Exists(localRootPath))
            {
                Directory.CreateDirectory(localRootPath);
            }

            if (this._ftpDownloadConfig.CleanupLocalDirectory)
            {
                CleanupInstallDirectory(localRootPath);
            }

            _logger.LogInformation( "Enumerate local Objects Begin");


            var ftpListOption = _ftpDownloadConfig.IncludeSubDirectories ? FtpListOption.Recursive : FtpListOption.Auto;

            var remoteFileListDict = (await ftpClient.GetListing(_ftpDownloadConfig.RemoteDirectory, ftpListOption, cancellationToken)).Where(x => x.Type == FtpObjectType.File).ToDictionary<FtpListItem, string>(x => x.FullName);

            _logger.LogInformation( "Enumerate Ftp objects Begin");
            foreach (var item in remoteFileListDict)
            {
                _logger.LogInformation( $"{item}");
            }
            _logger.LogInformation( "Enumerate Ftp objects End");

            foreach (var item in remoteFileListDict)
            {
                var localFilePath = Path.Combine(localRootPath, Path.GetRelativePath(_ftpDownloadConfig.RemoteDirectory, item.Key));
                localFilePath = Path.GetFullPath(localFilePath);
                FtpStatus ftpStatus = FtpStatus.Failed;
                int retryTimes = 0;
                do
                {
                    ftpStatus = await DownloadFileAsync(ftpClient, item.Value, localFilePath, cancellationToken);
                    retryTimes++;
                    if (ftpStatus == FtpStatus.Failed)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                } while (
                !cancellationToken.IsCancellationRequested
                &&
                ftpStatus == FtpStatus.Failed
                &&
                (this._ftpDownloadConfig.RetryTimes == 0 ||

                (this._ftpDownloadConfig.RetryTimes > 0
                &&
                retryTimes <= this._ftpDownloadConfig.RetryTimes
                )));

            }

            _logger.LogInformation( $"successCount:{successCount} skippedCount:{skippedCount}");
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
                if (!this._ftpDownloadConfig.CleanupLocalDirectory && this._ftpDownloadConfig.FileExistsTime > 0 && CompareFileInfo(localFilePath, remoteFileInfo))
                {
                    ftpStatus = await ftpClient.DownloadFile(localFilePath,
                         remoteFileInfo.FullName,
                         (FtpLocalExists)this._ftpDownloadConfig.FtpFileExists,
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

                    _logger.LogInformation( $"FullName:{localFilePath} CreationTime:{File.GetCreationTime(localFilePath)} LastWriteTime:{File.GetLastWriteTime(localFilePath)}  success");
                    successCount++;
                }
                else if (ftpStatus == FtpStatus.Skipped)
                {
                    _logger.LogInformation( $"FullName:{localFilePath}  skipped");
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError( ex.ToString());
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
            switch (_ftpDownloadConfig.FileExistsTimeUnit)
            {
                case FileExistsTimeUnit.Seconds:
                    timeSpan = TimeSpan.FromSeconds(_ftpDownloadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Minutes:
                    timeSpan = TimeSpan.FromMinutes(_ftpDownloadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Hours:
                    timeSpan = TimeSpan.FromHours(_ftpDownloadConfig.FileExistsTime);
                    break;
                case FileExistsTimeUnit.Days:
                    timeSpan = TimeSpan.FromDays(_ftpDownloadConfig.FileExistsTime);
                    break;
                default:
                    break;
            }

            var lastWriteTime = File.GetLastWriteTime(localFilePath);
            switch (_ftpDownloadConfig.FileExistsTimeRange)
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
