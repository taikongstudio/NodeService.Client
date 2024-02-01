using FluentFTP;
using JobsWorker.Shared.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Net;

namespace JobsWorkerNodeService.Jobs
{
    internal class UploadlogsToFtpServerJob : JobBase
    {
        public UploadlogsToFtpServerJob()
        {
   
        }

        public override async Task Execute(IJobExecutionContext context)
        {
            await this.UploadLogsToFtpServer();
        }

        private async Task UploadLogsToFtpServer()
        {
            try
            {
                var myFtpProgress = new MyFtpProgress(this.Logger);
                await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 30)));
                var server = options[nameof(FtpConnectConfig.host)];
                var username = options[nameof(FtpConnectConfig.username)];
                var password = options[nameof(FtpConnectConfig.password)];
                var localLogDirectory = options[nameof(LogUploadConfig.localLogDirectory)];
                var remoteLogDirectoryFormat = options[nameof(LogUploadConfig.remoteLogDirectoryFormat)];
                using (var ftpClient = new AsyncFtpClient(server, username, password))
                {
                    await ftpClient.AutoConnect();
                    if (!Directory.Exists(localLogDirectory))
                    {
                        localLogDirectory = Path.Combine(AppContext.BaseDirectory, localLogDirectory);
                    }

                    string[] localLogFiles = Directory.GetFiles(localLogDirectory, "*.log");
                    var dnsName = Dns.GetHostName();
                    string remoteDir = string.Format(remoteLogDirectoryFormat, dnsName);
                    var rootDir = "JobsWorkerDaemonService";
                    if (!await ftpClient.DirectoryExists(rootDir))
                    {
                         await ftpClient.CreateDirectory(rootDir);
                    }
                    string uploadedLogsBat = "uploadedlogs.bat";
                    var localFtpUploadRecordPath = Path.Combine(AppContext.BaseDirectory, "logs", uploadedLogsBat);
                    var remoteUploadRecordDir = Path.Combine(rootDir, dnsName, uploadedLogsBat);

                    HashSet<string> pathHashSet = new HashSet<string>();
                    try
                    {
                        if (await ftpClient.FileExists(remoteUploadRecordDir))
                        {
                            using (var memoryStream = new MemoryStream())
                            using (var streamReader = new StreamReader(memoryStream))
                            {
                                if (await ftpClient.DownloadStream(memoryStream, remoteUploadRecordDir))
                                {
                                    memoryStream.Position = 0;
                                    while (!streamReader.EndOfStream)
                                    {
                                        var line = streamReader.ReadLine();
                                        if (line.Contains($"-{DateTime.Now.ToString("yyyy--MM--dd")}"))
                                        {
                                            continue;
                                        }
                                        if (line != null)
                                        {
                                            pathHashSet.Add(line);
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

                    int addedCount = 0;

                    foreach (var localFilePath in localLogFiles)
                    {
                        if (pathHashSet.Contains(localFilePath))
                        {
                            continue;
                        }
                        var fileName = Path.GetFileName(localFilePath);
                        var remoteFilePath = Path.Combine(remoteDir, fileName);

                        bool retry = false;
                        try
                        {

                           await ftpClient.UploadFile(localFilePath, remoteFilePath, FtpRemoteExists.Overwrite, true, FtpVerify.None, myFtpProgress);
                            pathHashSet.Add(localFilePath);
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
                    if (addedCount > 0)
                    {
                        File.WriteAllLines(localFtpUploadRecordPath, pathHashSet.ToArray());
                       await ftpClient.UploadFile(localFtpUploadRecordPath, remoteUploadRecordDir);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
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
