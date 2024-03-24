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
        public FtpUploadConfigModel FtpUploadConfig { get; set; }

        private readonly MyFtpProgress _myFtpProgress;

        public ILogger Logger { get; set; }

        public FtpUploadConfigExecutor(
            MyFtpProgress myFtpProgress,
            FtpUploadConfigModel ftpTaskConfig,
            ILogger logger)
        {
            _myFtpProgress = myFtpProgress;
            FtpUploadConfig = ftpTaskConfig;
            Logger = logger;
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
            Logger.LogInformation($"Execute config on host {FtpUploadConfig.FtpConfig.Host} port {FtpUploadConfig.FtpConfig.Port}" +
                $" username {FtpUploadConfig.FtpConfig.Username} password {FtpUploadConfig.FtpConfig.Password}");

            var hostName = Dns.GetHostName();


            string rootPath = FtpUploadConfig.LocalDirectory;

            if (FtpUploadConfig.IsLocalDirectoryUseMapping)
            {
                var localDirectoryConfig = FtpUploadConfig.LocalDirectoryMappingConfig;
                var path = localDirectoryConfig.Entries.FirstOrDefault(x => x.Name == FtpUploadConfig.LocalDirectory)?.Value;
                rootPath = path;
            }
            if (!Directory.Exists(rootPath))
            {
                Logger.LogInformation($"Could not found directory:{rootPath}");
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

            Logger.LogInformation("Enumerate local Objects Begin");
            foreach (var localFile in localFiles)
            {
                var fileInfo = new FileInfo(localFile);
                Logger.LogInformation($"FullName:{fileInfo.FullName},ModifiedTime:{fileInfo.LastWriteTime},Length:{fileInfo.Length}");
            }
            Logger.LogInformation("Enumerate local Objects End");

            if (FtpUploadConfig.Filters != null && FtpUploadConfig.Filters.Any())
            {
                localFiles = localFiles.Where(x =>
                {
                    foreach (var filter in FtpUploadConfig.Filters)
                    {
                        if (filter.Value == null)
                        {
                            continue;
                        }
                        if (x.Contains(filter.Value))
                        {
                            return true;
                        }
                    }
                    return false;
                }).ToArray();
            }


            FtpUploadConfig.RemoteDirectory = FtpUploadConfig.RemoteDirectory.Replace("$(HostName)", hostName).Replace("\\", "/");

            if (!FtpUploadConfig.RemoteDirectory.StartsWith('/'))
            {
                FtpUploadConfig.RemoteDirectory = '/' + FtpUploadConfig.RemoteDirectory;
            }

            var remoteFileListDict = (await ftpClient.GetListing(FtpUploadConfig.RemoteDirectory,
                FtpUploadConfig.IncludeSubDirectories ? FtpListOption.Recursive : FtpListOption.Auto
                , cancellationToken)).ToDictionary<FtpListItem, string>(x => x.FullName);



            Logger.LogInformation("Enumerate Ftp objects Begin");
            foreach (var item in remoteFileListDict)
            {
                Logger.LogInformation($"FullName:{item.Key},ModifiedTime:{item.Value.Modified},Length:{item.Value.Size}");
            }
            Logger.LogInformation("Enumerate Ftp objects End");


            foreach (var fileGroup in localFiles.GroupBy<string, string>(x => Path.GetDirectoryName(x)))
            {
                foreach (var localFilePath in fileGroup)
                {
                    string remoteFilePath = CaculateRemoteFilePath(FtpUploadConfig.RemoteDirectory, rootPath, localFilePath);


                    FtpStatus ftpStatus = FtpStatus.Failed;
                    int retryTimes = 0;
                    do
                    {
                        ftpStatus = await UploadFileAsync(ftpClient, remoteFileListDict, localFilePath, remoteFilePath, cancellationToken);
                        retryTimes++;
                        if (ftpStatus == FtpStatus.Failed)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
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
                        Logger.LogInformation($"Upload success:LocalPath:{localFilePath} RemotePath:{remoteFilePath}");
                        var lastWriteTime = File.GetLastWriteTime(localFilePath);
                        await ftpClient.SetModifiedTime(remoteFilePath, lastWriteTime, cancellationToken);
                        Logger.LogInformation($"ModifiedTime:{lastWriteTime}");
                    }
                    else if (ftpStatus == FtpStatus.Skipped)
                    {
                        skippedCount++;
                        Logger.LogInformation($"Skip :LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
                    }
                }
            }
            Logger.LogInformation($"uploadedCount:{uploadedCount} skippedCount:{skippedCount} overidedCount:{overidedCount}");

        }

        private async Task<FtpStatus> UploadFileAsync(AsyncFtpClient ftpClient, Dictionary<string, FtpListItem> remoteFileDict, string localFilePath, string remoteFilePath, CancellationToken cancellationToken = default)
        {
            FtpStatus ftpStatus = FtpStatus.Failed;
            try
            {
                if (!FtpUploadConfig.CleanupRemoteDirectory
                    && this.FtpUploadConfig.FileExistsTime > 0
                    && remoteFileDict.TryGetValue(remoteFilePath, out var ftpListItem)
                    && CompareFileInfo(localFilePath, ftpListItem))
                {
                    ftpStatus = await ftpClient.UploadFile(localFilePath,
                          remoteFilePath,
                          (FtpRemoteExists)this.FtpUploadConfig.FtpFileExists,
                          true,
                          FtpVerify.None,
                          _myFtpProgress, token: cancellationToken);
                }
                else
                {
                    ftpStatus = await ftpClient.UploadFile(localFilePath,
                         remoteFilePath,
                         FtpRemoteExists.Skip,
                         true,
                         FtpVerify.None,
                         _myFtpProgress, token: cancellationToken);
                }

            }
            catch (Exception ex)
            {
                Logger.LogInformation(ex.ToString());
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return ftpStatus;
        }

        private bool CompareFileInfo(string localFilePath, FtpListItem remoteFileInfo)
        {
            var lastWriteTime = File.GetLastWriteTime(localFilePath);
            bool compareDateTime = false;
            TimeSpan timeSpan = TimeSpan.MinValue;

            Logger.LogInformation($"LocalFileInfo{localFilePath} RemoteFileInfo:{remoteFileInfo.FullName} LastWriteTime:{lastWriteTime} Modified:{remoteFileInfo.Modified.Date} ");


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
                    compareDateTime = remoteFileInfo.Modified - lastWriteTime <= timeSpan;
                    break;
                case FileExistsTimeRange.OutOfRange:
                    compareDateTime = remoteFileInfo.Modified - lastWriteTime > timeSpan;
                    break;
                default:
                    break;
            }
            return compareDateTime;
        }

        private static string CaculateRemoteFilePath(string remoteRootDir, string rootPath, string? localFilePath)
        {
            var parentDir = Path.GetDirectoryName(localFilePath);
            var relativePath = Path.GetRelativePath(rootPath, parentDir);
            var fileName = Path.GetFileName(localFilePath);
            var remoteFilePath = Path.Combine(remoteRootDir, relativePath, fileName);
            remoteFilePath = remoteFilePath.Replace("\\", "/");
            remoteFilePath = remoteFilePath.Replace("/./", "/");
            return remoteFilePath;
        }

    }
}
