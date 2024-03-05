using FluentFTP;
using JobsWorker.Shared.DataModels;
using JobsWorkerNodeService.Jobs.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Net;

namespace JobsWorkerNodeService.Jobs
{
    internal class UploadAppLogsToFtpServerJob : JobBase
    {
        public UploadAppLogsToFtpServerJob()
        {

        }

        public override async Task Execute(IJobExecutionContext context)
        {
            await this.UploadLogsToFtpServerImpl();
        }

        private async Task UploadLogsToFtpServerImpl()
        {
            try
            {
                UploadlogsToFtpServerJobOptions options = this.JobScheduleConfig.GetOptions<UploadlogsToFtpServerJobOptions>();
                var myFtpProgress = new MyFtpProgress(this.Logger);
                var dnsName = Dns.GetHostName();
                List<LogUploadConfigModel> uploadLogConfigList = new List<LogUploadConfigModel>();

                foreach (var logUploadConfigName in options.logUploadConfigNames)
                {
                    var logUploadConfig = this.NodeConfigTemplate.FindLogUploadConfig(logUploadConfigName);
                    uploadLogConfigList.Add(logUploadConfig);
                }

                foreach (var uploadLogConfigGroup in uploadLogConfigList.GroupBy(x => x.FtpConfig))
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

                            var localLogDirectories = logUploadConfig.LocalDirectories;
                            var remoteLogDirectoryFormat = logUploadConfig.RemoteDirectory;

                            string remoteLogRootDir = string.Format(remoteLogDirectoryFormat, dnsName);


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
                                    this.Logger.LogWarning($"Cound not find directory:{myLocalLogDirectory}");
                                    continue;
                                }

                                string[] localLogFiles = Directory.GetFiles(myLocalLogDirectory, "*.log", new EnumerationOptions()
                                {
                                    RecurseSubdirectories = true,
                                }).Select(x => x.Replace("\\", "/")).ToArray();

                                if (!await ftpClient.DirectoryExists(remoteLogRootDir))
                                {
                                    await ftpClient.CreateDirectory(remoteLogRootDir);
                                }

                                HashSet<string> pathHashSet = await DownloadUploadFilesRecordFromFtpServer(ftpClient, remoteLogRootDir);

                                int addedCount = 0;

                                foreach (var localFilePath in localLogFiles)
                                {
                                    FileInfo fileInfo = new FileInfo(localFilePath);
                                    if (logUploadConfig.SizeLimitInBytes > 0 && fileInfo.Length > logUploadConfig.SizeLimitInBytes)
                                    {
                                        this.Logger.LogInformation($"Skip upload {localFilePath} sizelimit:{logUploadConfig.SizeLimitInBytes} filesize:{fileInfo.Length}");
                                        continue;
                                    }
                                    var timeSpan = (DateTime.Now - fileInfo.LastWriteTime);
                                    if (logUploadConfig.TimeLimitInSeconds > 0 && timeSpan.TotalSeconds > logUploadConfig.TimeLimitInSeconds)
                                    {
                                        this.Logger.LogInformation($"Skip upload {localFilePath} timeSecondsLimit:{logUploadConfig.TimeLimitInSeconds} timeSpan:{timeSpan}");
                                        continue;
                                    }
                                    string releativePath = Path.GetRelativePath(myLocalLogDirectory, localFilePath).Replace("\\", "/");
                                    if (pathHashSet.Contains(releativePath))
                                    {
                                        continue;
                                    }
                                    var fileName = Path.GetFileName(localFilePath);
                                    var remoteFilePath = Path.Combine(remoteLogRootDir, fileName);

                                    bool retry = false;
                                    try
                                    {

                                        await ftpClient.UploadFile(localFilePath, remoteFilePath, FtpRemoteExists.Overwrite, true, FtpVerify.None, myFtpProgress);
                                        pathHashSet.Add(releativePath);
                                        addedCount++;
                                    }
                                    catch (Exception ex)
                                    {
                                        this.Logger.LogError(ex.ToString());
                                        retry = true;
                                    }



                                    if (retry)
                                    {
                                        try
                                        {

                                            using (var fileStream = File.Open(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                            {
                                                await ftpClient.UploadStream(fileStream, remoteFilePath, FtpRemoteExists.Resume, true, myFtpProgress);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            this.Logger.LogError(ex.ToString());
                                        }
                                    }
                                }
                            }

                        }
                    }

                }



            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
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
                var allFileNames = (await ftpClient.GetListing(remoteLogRootDir));
                pathHashSet = allFileNames
                    .Where(x => !x.Name.Contains(exclude))
                    .Select(x => Path.GetRelativePath(remoteLogRootDir, x.FullName))
                    .ToHashSet();

            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

            return pathHashSet;
        }

        private void UploadProgress(FtpProgress ftpProgress)
        {
            if (ftpProgress.Progress == 100)
            {
                this.Logger.LogInformation($"LocalPath:{ftpProgress.LocalPath}=>RemotePath:{ftpProgress.RemotePath} Progress:{ftpProgress.Progress} TransferSpeed:{ftpProgress.TransferSpeed}");
            }
        }

    }
}
