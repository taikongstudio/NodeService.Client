using FluentFTP;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System.Net;

namespace NodeService.WindowsService.Services
{
    public class LogUploadJob : Job
    {
        private readonly MyFtpProgress _myFtpProgress;
        private readonly string _dnsName;

        public LogUploadJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {
            _myFtpProgress = new MyFtpProgress(ProcessFtpProgress);
            _dnsName = Dns.GetHostName();
        }

        void ProcessFtpProgress(FtpProgress progress)
        {
            if (progress.Progress == 100)
            {
                Logger.LogInformation($"LocalPath:{progress.LocalPath}=>RemotePath:{progress.RemotePath} Progress:{progress.Progress} TransferSpeed:{progress.TransferSpeed}");
            }

        }

        private static async Task UploadRecords(AsyncFtpClient ftpClient, string remoteUploadRecordDir, HashSet<string> pathHashSet, int addedCount)
        {
            if (addedCount > 0)
            {
                using var memoryStream = new MemoryStream();
                using var streamWriter = new StreamWriter(memoryStream);
                foreach (var path in pathHashSet)
                {
                    streamWriter.WriteLine(path);
                }
                memoryStream.Position = 0;
                await ftpClient.UploadStream(memoryStream, remoteUploadRecordDir);
            }
        }

        private async Task<HashSet<string>> DownloadUploadFilesRecordFromFtpServer(AsyncFtpClient ftpClient, string remoteLogRootDir)
        {
            HashSet<string> pathHashSet = new HashSet<string>();
            try
            {
                string exclude = $"-{DateTime.Now.ToString("yyyy-MM-dd")}";
                var allFileNames = await ftpClient.GetListing(remoteLogRootDir);
                pathHashSet = allFileNames
                    .Where(x => !x.Name.Contains(exclude))
                    .Select(x => Path.GetRelativePath(remoteLogRootDir, x.FullName))
                    .ToHashSet();

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            return pathHashSet;
        }


        private async Task ExecuteLogUploadConfigAsync(AsyncFtpClient ftpClient, LogUploadConfigModel? logUploadConfig)
        {
            var localLogDirectories = logUploadConfig.LocalDirectories;

            string remoteLogRootDir = logUploadConfig.RemoteDirectory.Replace("$(HostName)", _dnsName);
            if (!await ftpClient.DirectoryExists(remoteLogRootDir))
            {
                await ftpClient.CreateDirectory(remoteLogRootDir);
            }

            await ftpClient.AutoConnect();
            foreach (var localLogDirectory in localLogDirectories)
            {
                string myLocalLogDirectory = localLogDirectory.Value;
                if (!Directory.Exists(myLocalLogDirectory))
                {
                    myLocalLogDirectory = Path.Combine(AppContext.BaseDirectory, myLocalLogDirectory);
                }

                if (!Directory.Exists(myLocalLogDirectory))
                {
                    Logger.LogWarning($"Cound not find directory:{myLocalLogDirectory}");
                    continue;
                }

                string[] localLogFiles = Directory.GetFiles(myLocalLogDirectory, logUploadConfig.SearchPattern, new EnumerationOptions()
                {
                    MatchCasing = logUploadConfig.MatchCasing,
                    RecurseSubdirectories = logUploadConfig.IncludeSubDirectories,
                }).Select(x => x.Replace("\\", "/")).ToArray();

                HashSet<string> pathHashSet = await DownloadUploadFilesRecordFromFtpServer(ftpClient, remoteLogRootDir);

                int addedCount = 0;

                foreach (var localFilePath in localLogFiles)
                {
                    FileInfo fileInfo = new FileInfo(localFilePath);
                    if (logUploadConfig.SizeLimitInBytes > 0 && fileInfo.Length > logUploadConfig.SizeLimitInBytes)
                    {
                        Logger.LogInformation($"Skip upload {localFilePath} sizelimit:{logUploadConfig.SizeLimitInBytes} filesize:{fileInfo.Length}");
                        continue;
                    }
                    var timeSpan = DateTime.Now - fileInfo.LastWriteTime;
                    if (logUploadConfig.TimeLimitInSeconds > 0 && timeSpan.TotalSeconds > logUploadConfig.TimeLimitInSeconds)
                    {
                        Logger.LogInformation($"Skip upload {localFilePath} timeSecondsLimit:{logUploadConfig.TimeLimitInSeconds} timeSpan:{timeSpan}");
                        continue;
                    }
                    string releativePath = Path.GetRelativePath(myLocalLogDirectory, localFilePath).Replace("\\", "/");
                    if (pathHashSet.Contains(releativePath))
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
                            Logger.LogInformation($"Upload log:{localFilePath} to ftp {remoteFilePath}");
                            pathHashSet.Add(releativePath);
                            addedCount++;
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                        retry = true;
                    }



                    if (retry)
                    {
                        try
                        {

                            using (var fileStream = File.Open(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                await ftpClient.UploadStream(fileStream, remoteFilePath, FtpRemoteExists.Resume, true, _myFtpProgress);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex.ToString());
                        }
                    }
                }
            }
        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                LogUploadJobOptions logUploadOptions = new LogUploadJobOptions();
                await logUploadOptions.InitAsync(this.JobScheduleConfig, ApiService);

                foreach (var uploadLogConfigGroup in logUploadOptions.LogUploadConfigs.GroupBy(x => x.FtpConfig))
                {
                    var ftpConfig = uploadLogConfigGroup.Key;
                    using (var ftpClient = new AsyncFtpClient(
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
                       }))
                    {
                        foreach (var logUploadConfig in uploadLogConfigGroup)
                        {
                            await ExecuteLogUploadConfigAsync(ftpClient, logUploadConfig);

                        }
                    }

                }



            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
    }
}
