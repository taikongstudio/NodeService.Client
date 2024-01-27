
using FluentFTP;
using JobsWorkerDaemonService.Models;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorkerDaemonService.Jobs
{
    internal class UploadlogsToFtpServerJob : JobBase
    {
        public override Task Execute(IJobExecutionContext context)
        {
            UploadLogsToFtpServer();
            return Task.CompletedTask;
        }

        private async void UploadLogsToFtpServer()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 30)));
                var server = this.Arguments[nameof(FtpServerConfig.server)];
                var username = this.Arguments[nameof(FtpServerConfig.username)];
                var password = this.Arguments[nameof(FtpServerConfig.password)];
                var localLogDirectory = this.Arguments[nameof(FtpServerConfig.localLogDirectory)];
                var remoteLogDirectoryFormat = this.Arguments[nameof(FtpServerConfig.remoteLogDirectoryFormat)];
                using (var ftpClient = new FtpClient(server, username, password))
                {
                    ftpClient.AutoConnect();
                    if (!Directory.Exists(localLogDirectory))
                    {
                        localLogDirectory = Path.Combine(AppContext.BaseDirectory, localLogDirectory);
                    }

                    string[] localLogFiles = Directory.GetFiles(localLogDirectory, "*.log");
                    var dnsName = Dns.GetHostName();
                    string remoteDir = string.Format(remoteLogDirectoryFormat, dnsName);
                    var rootDir = "JobsWorkerDaemonService";
                    if (!ftpClient.DirectoryExists(rootDir))
                    {
                        ftpClient.CreateDirectory(rootDir);
                    }
                    string uploadedLogsBat = "uploadedlogs.bat";
                    var localFtpUploadRecordPath = Path.Combine(AppContext.BaseDirectory, "config", uploadedLogsBat);
                    var remoteUploadRecordDir = Path.Combine(rootDir, dnsName, uploadedLogsBat);

                    HashSet<string> pathHashSet = new HashSet<string>();
                    try
                    {
                        if (ftpClient.FileExists(remoteUploadRecordDir))
                        {
                            using (var memoryStream = new MemoryStream())
                            using (var streamReader = new StreamReader(memoryStream))
                            {
                                if (ftpClient.DownloadStream(memoryStream, remoteUploadRecordDir))
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
                        Logger.LogError(ex.ToString());
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

                            ftpClient.UploadFile(localFilePath, remoteFilePath, FtpRemoteExists.Overwrite, true, FtpVerify.None, UploadProgress);
                            pathHashSet.Add(localFilePath);
                            addedCount++;
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
                                    ftpClient.UploadStream(fileStream, remoteFilePath, FtpRemoteExists.Resume, true, UploadProgress);
                                    pathHashSet.Add(localFilePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex.ToString());
                            }
                        }
                    }
                    if (addedCount > 0)
                    {
                        File.WriteAllLines(localFtpUploadRecordPath, pathHashSet.ToArray());
                        ftpClient.UploadFile(localFtpUploadRecordPath, remoteUploadRecordDir);
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
                Logger.LogInformation($"LocalPath:{ftpProgress.LocalPath}=>RemotePath:{ftpProgress.RemotePath} Progress:{ftpProgress.Progress} TransferSpeed:{ftpProgress.TransferSpeed}");

            }
        }

    }
}
