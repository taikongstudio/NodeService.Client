using FluentFTP;
using JobsWorkerWebService.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Net;

namespace JobsWorkerNodeService.Jobs
{
    internal class UploadAppLogsToFtpServerJob : JobBase
    {
        public override async Task Execute(IJobExecutionContext context)
        {
            await UploadLogsToFtpServer();
        }

        private async Task UploadLogsToFtpServer()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 30)));
                var server = Arguments[nameof(DeviceConfiguration.host)];
                var username = Arguments[nameof(DeviceConfiguration.username)];
                var password = Arguments[nameof(DeviceConfiguration.password)];
                var localLogDirectory = Arguments[nameof(DeviceConfiguration.localLogDirectory)];
                var remoteLogDirectoryFormat = Arguments[nameof(DeviceConfiguration.remoteLogDirectoryFormat)];
                var searchPattern = Arguments["searchPattern"];
                var appName = Arguments["appName"];
                var timelimithour = int.Parse(Arguments["timespanhour"]);
                var sizelimitbytes = int.Parse(Arguments["sizelimitbytes"]);
                using (var ftpClient = new FtpClient(server, username, password))
                {
                    ftpClient.AutoConnect();
                    if (!Directory.Exists(localLogDirectory))
                    {
                        localLogDirectory = Path.Combine(AppContext.BaseDirectory, localLogDirectory);
                    }

                    string[] localLogFiles = Directory.GetFiles(localLogDirectory, searchPattern);
                    var dnsName = Dns.GetHostName();
                    string remoteDir = string.Format(remoteLogDirectoryFormat, dnsName);
                    if (!ftpClient.DirectoryExists(remoteDir))
                    {
                        ftpClient.CreateDirectory(remoteDir);
                    }
                    string uploadedLogsBat = appName + "uploadedlogs.bat";
                    var localFtpUploadRecordPath = Path.Combine(AppContext.BaseDirectory, "logs", uploadedLogsBat);
                    var remoteUploadLogsBat = Path.Combine(remoteDir, uploadedLogsBat);

                    HashSet<string> pathHashSet = new HashSet<string>();
                    try
                    {
                        if (ftpClient.FileExists(remoteUploadLogsBat))
                        {
                            using (var memoryStream = new MemoryStream())
                            using (var streamReader = new StreamReader(memoryStream))
                            {
                                if (ftpClient.DownloadStream(memoryStream, remoteUploadLogsBat))
                                {
                                    memoryStream.Position = 0;
                                    while (!streamReader.EndOfStream)
                                    {
                                        var line = streamReader.ReadLine();
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
                        var fileinfo = new FileInfo(localFilePath);
                        if (DateTime.Now - fileinfo.LastWriteTime > TimeSpan.FromHours(timelimithour))
                        {
                            Logger.LogInformation($"Skip {localFilePath},LastWriteTIme{fileinfo.LastWriteTime}");
                            continue;
                        }
                        if (fileinfo.Length > sizelimitbytes)
                        {
                            Logger.LogInformation($"Skip {localFilePath},Size{fileinfo.Length}");
                            continue;
                        }
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
                        ftpClient.UploadFile(localFtpUploadRecordPath, remoteUploadLogsBat);
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
