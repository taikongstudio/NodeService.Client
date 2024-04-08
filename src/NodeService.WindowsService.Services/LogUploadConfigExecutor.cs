using FluentFTP;
using Microsoft.Extensions.Logging;
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class LogUploadConfigExecutor
    {
        public LogUploadConfigModel LogUploadConfig { get; private set; }

        private IProgress<FtpProgress> _progress;

        private readonly ILogger _logger;

        public LogUploadConfigExecutor(
            IProgress<FtpProgress> progress,
            LogUploadConfigModel logUploadConfig,
            ILogger logger)
        {
            _progress = progress;
            _logger = logger;
            LogUploadConfig = logUploadConfig;
        }

        public async Task ExecutionAsync(CancellationToken cancellationToken = default)
        {
            var ftpConfig = this.LogUploadConfig.FtpConfig;
            using var ftpClient = new AsyncFtpClient(
                ftpConfig.Host,
                ftpConfig.Username,
                ftpConfig.Password,
                ftpConfig.Port,
                new FtpConfig()
                {
                    ReadTimeout = ftpConfig.ReadTimeout,
                    ConnectTimeout = ftpConfig.ConnectTimeout,
                    DataConnectionConnectTimeout = ftpConfig.DataConnectionConnectTimeout,
                    DataConnectionReadTimeout = ftpConfig.DataConnectionReadTimeout,
                });

            await ftpClient.AutoConnect(cancellationToken);

            var localLogDirectories = LogUploadConfig.LocalDirectories;

            string remoteLogRootDir = LogUploadConfig.RemoteDirectory.Replace("$(HostName)", Dns.GetHostName());
            if (!remoteLogRootDir.StartsWith('/'))
            {
                remoteLogRootDir = '/' + remoteLogRootDir;
            }
            if (!await ftpClient.DirectoryExists(remoteLogRootDir, cancellationToken))
            {
                await ftpClient.CreateDirectory(remoteLogRootDir, cancellationToken);
            }


            foreach (var localLogDirectory in localLogDirectories)
            {
                string myLocalLogDirectory = localLogDirectory.Value;

                if (!Directory.Exists(myLocalLogDirectory))
                {
                    _logger.LogWarning($"Cound not find directory:{myLocalLogDirectory}");
                    continue;
                }

                string[] localLogFiles = Directory.GetFiles(myLocalLogDirectory, LogUploadConfig.SearchPattern,
                    new EnumerationOptions()
                    {
                        MatchCasing = LogUploadConfig.MatchCasing,
                        RecurseSubdirectories = LogUploadConfig.IncludeSubDirectories,
                    }).Select(x => x.Replace('\\', '/')).ToArray();

                Dictionary<string, FtpListItem> dict = await GetRemoteFilesAsync(ftpClient, remoteLogRootDir, cancellationToken);

                int addedCount = 0;

                foreach (var localFilePath in localLogFiles)
                {
                    FileInfo fileInfo = new FileInfo(localFilePath);
                    if (LogUploadConfig.SizeLimitInBytes > 0 && fileInfo.Length > LogUploadConfig.SizeLimitInBytes)
                    {
                        _logger.LogInformation($"Skip upload {localFilePath} sizelimit:{LogUploadConfig.SizeLimitInBytes} filesize:{fileInfo.Length}");
                        continue;
                    }
                    var timeSpan = DateTime.Now - fileInfo.LastWriteTime;
                    if (LogUploadConfig.TimeLimitInSeconds > 0 && timeSpan.TotalSeconds > LogUploadConfig.TimeLimitInSeconds)
                    {
                        _logger.LogInformation($"Skip upload {localFilePath} timeSecondsLimit:{LogUploadConfig.TimeLimitInSeconds} timeSpan:{timeSpan}");
                        continue;
                    }
                    string releativePath = Path.GetRelativePath(myLocalLogDirectory, localFilePath).Replace("\\", "/");
                    if (dict.ContainsKey(releativePath))
                    {
                        continue;
                    }

                    var remoteFilePath = Path.Combine(remoteLogRootDir, releativePath);

                    bool retry = false;
                    try
                    {

                        var ftpStatus = await ftpClient.UploadFile(localFilePath, remoteFilePath, FtpRemoteExists.Overwrite, true, FtpVerify.None, _progress);
                        if (ftpStatus == FtpStatus.Success)
                        {
                            _logger.LogInformation($"Upload log:{localFilePath} to {remoteFilePath}");
                            dict.Add(releativePath, null);
                            addedCount++;
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                        retry = true;
                    }



                    if (retry)
                    {
                        try
                        {

                            using var fileStream = File.Open(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            await ftpClient.UploadStream(fileStream, remoteFilePath, FtpRemoteExists.Resume, true, _progress, token: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.ToString());
                        }
                    }
                }
            }
        }

        private async Task<Dictionary<string, FtpListItem>> GetRemoteFilesAsync(
            AsyncFtpClient ftpClient,
            string remoteLogRootDir,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, FtpListItem> dict = new Dictionary<string, FtpListItem>();
            try
            {
                string exclude = $"{DateTime.Now:yyyy-MM-dd}";
                var listOption = this.LogUploadConfig.IncludeSubDirectories ? FtpListOption.Recursive : FtpListOption.Auto;
                var allFileItems = await ftpClient.GetListing(remoteLogRootDir, listOption, cancellationToken);
                dict = allFileItems
                    .Where(x => !x.Name.Contains(exclude))
                    .ToDictionary(x => GetRelativePath(remoteLogRootDir, x.FullName));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            return dict;
        }

        private string GetRelativePath(string rootPath, string fullName)
        {
            var path = Path.GetRelativePath(rootPath, fullName);
            return path;
        }

    }
}
