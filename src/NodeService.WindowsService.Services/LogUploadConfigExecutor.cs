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
        public readonly LogUploadConfigModel _logUploadConfig;

        private readonly MyFtpProgress _myFtpProgress;

        public readonly ILogger _logger;

        private readonly EventId _eventId;

        public LogUploadConfigExecutor(
            EventId eventId,
            MyFtpProgress myFtpProgress,
            LogUploadConfigModel logUploadConfig,
            ILogger logger)
        {
            _myFtpProgress = myFtpProgress;
            _eventId = eventId;
            _logUploadConfig = logUploadConfig;
            _logger = logger;
        }

        public async Task ExecutionAsync(CancellationToken cancellationToken = default)
        {
            var ftpConfig = this._logUploadConfig.FtpConfig;
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

            var localLogDirectories = _logUploadConfig.LocalDirectories;

            string remoteLogRootDir = _logUploadConfig.RemoteDirectory.Replace("$(HostName)", Dns.GetHostName());
            if (!await ftpClient.DirectoryExists(remoteLogRootDir, cancellationToken))
            {
                await ftpClient.CreateDirectory(remoteLogRootDir, cancellationToken);
            }

            await ftpClient.AutoConnect(cancellationToken);
            foreach (var localLogDirectory in localLogDirectories)
            {
                string myLocalLogDirectory = localLogDirectory.Value;

                if (!Directory.Exists(myLocalLogDirectory))
                {
                    _logger.LogWarning(_eventId, $"Cound not find directory:{myLocalLogDirectory}");
                    continue;
                }

                string[] localLogFiles = Directory.GetFiles(myLocalLogDirectory, _logUploadConfig.SearchPattern,
                    new EnumerationOptions()
                    {
                        MatchCasing = _logUploadConfig.MatchCasing,
                        RecurseSubdirectories = _logUploadConfig.IncludeSubDirectories,
                    }).Select(x => x.Replace('\\', '/')).ToArray();

                Dictionary<string, FtpListItem> dict = await GetRemoteFiles(ftpClient, remoteLogRootDir, cancellationToken);

                int addedCount = 0;

                foreach (var localFilePath in localLogFiles)
                {
                    FileInfo fileInfo = new FileInfo(localFilePath);
                    if (_logUploadConfig.SizeLimitInBytes > 0 && fileInfo.Length > _logUploadConfig.SizeLimitInBytes)
                    {
                        _logger.LogInformation( $"Skip upload {localFilePath} sizelimit:{_logUploadConfig.SizeLimitInBytes} filesize:{fileInfo.Length}");
                        continue;
                    }
                    var timeSpan = DateTime.Now - fileInfo.LastWriteTime;
                    if (_logUploadConfig.TimeLimitInSeconds > 0 && timeSpan.TotalSeconds > _logUploadConfig.TimeLimitInSeconds)
                    {
                        _logger.LogInformation( $"Skip upload {localFilePath} timeSecondsLimit:{_logUploadConfig.TimeLimitInSeconds} timeSpan:{timeSpan}");
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

                        var ftpStatus = await ftpClient.UploadFile(localFilePath, remoteFilePath, FtpRemoteExists.Overwrite, true, FtpVerify.None, _myFtpProgress);
                        if (ftpStatus == FtpStatus.Success)
                        {
                            _logger.LogInformation( $"Upload log:{localFilePath} to {remoteFilePath}");
                            dict.Add(releativePath, null);
                            addedCount++;
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError( ex.ToString());
                        retry = true;
                    }



                    if (retry)
                    {
                        try
                        {

                            using var fileStream = File.Open(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            await ftpClient.UploadStream(fileStream, remoteFilePath, FtpRemoteExists.Resume, true, _myFtpProgress, token: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError( ex.ToString());
                        }
                    }
                }
            }
        }

        private async Task<Dictionary<string, FtpListItem>> GetRemoteFiles(
            AsyncFtpClient ftpClient,
            string remoteLogRootDir,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, FtpListItem> dict = new Dictionary<string, FtpListItem>();
            try
            {
                string exclude = $"{DateTime.Now:yyyy-MM-dd}";
                var listOption = this._logUploadConfig.IncludeSubDirectories ? FtpListOption.Recursive : FtpListOption.Auto;
                var allFileItems = await ftpClient.GetListing(remoteLogRootDir, listOption, cancellationToken);
                dict = allFileItems
                    .Where(x => !x.Name.Contains(exclude))
                    .ToDictionary(x => Path.GetRelativePath(remoteLogRootDir, x.FullName));

            }
            catch (Exception ex)
            {
                _logger.LogError( ex.ToString());
            }

            return dict;
        }

    }
}
