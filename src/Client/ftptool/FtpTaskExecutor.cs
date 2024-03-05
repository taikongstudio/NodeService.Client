using FluentFTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ftptool
{
    public class FtpTaskExecutor
    {
        public FtpTaskExecutor(Response response)
        {
            Response = response;
        }

        public Response Response { get; set; }

        public void Run()
        {
            string currentTaskName = this.Response.defaultTaskName;
            do
            {
                var currentFtpTaskConfig = this.Response.ftpTasks.FirstOrDefault(x => x.taskName == currentTaskName);
                if (currentFtpTaskConfig == null)
                {
                    Console.WriteLine("break");
                    break;
                }
                ExecuteConfig(currentFtpTaskConfig);
                currentTaskName = currentFtpTaskConfig.nextTaskName;

            } while (true);
        }

        private void ExecuteConfig(FtpTaskConfig ftpTaskConfig)
        {
            using (var ftpClient = new FtpClient(ftpTaskConfig.host,
                ftpTaskConfig.username,
                ftpTaskConfig.password,
                ftpTaskConfig.port, new FtpConfig()
                {
                    ConnectTimeout = 60000,
                    ReadTimeout = 60000,
                    DataConnectionReadTimeout = 60000,
                    DataConnectionConnectTimeout = 60000,
                }))
            {
                ftpClient.AutoConnect();
                Console.WriteLine($"Execute config on host {ftpTaskConfig.host} port {ftpTaskConfig.port}" +
                    $" username {ftpTaskConfig.username} password {ftpTaskConfig.password}");
                using var memoryStream = new MemoryStream();
                if (!ftpClient.DownloadStream(memoryStream, this.Response.machineDirectoryMappingConfigPath))
                {
                    Console.WriteLine("Download machineDirectoryMappingConfig fail");
                    return;
                }
                memoryStream.Position = 0;

                var machineDirectoryMappingConfig = JsonSerializer.Deserialize<MachineDirectoryMappingConfig>(memoryStream);

                string rootPath = ftpTaskConfig.localDirectory;

                var hostName = Dns.GetHostName();

                if (ftpTaskConfig.isLocalDirectoryUseMapping)
                {
                    if (!machineDirectoryMappingConfig.TryGetHostConfig("*", out var defaultHostConfig))
                    {
                        Console.WriteLine("Could not found default config");
                        return;
                    }
                    if (!defaultHostConfig.TryGetDirectoryConfig(ftpTaskConfig.directoryConfigName, out var hostDirectoryConfig))
                    {
                        Console.WriteLine($"Could not found directory config {ftpTaskConfig.directoryConfigName}");
                        return;
                    }

                    if (!Directory.Exists(hostDirectoryConfig.root))
                    {
                        Console.WriteLine("Could not found machine directory mapping");
                        if (!machineDirectoryMappingConfig.TryGetHostConfig(hostName, out var hostConfig))
                        {
                            Console.WriteLine($"Could not found host config {hostName}");
                            return;
                        }
                        if (!hostConfig.TryGetDirectoryConfig(ftpTaskConfig.directoryConfigName, out hostDirectoryConfig))
                        {
                            Console.WriteLine($"Could not found directory config {ftpTaskConfig.directoryConfigName}");
                            return;
                        }
                    }
                    rootPath = Path.Combine(hostDirectoryConfig.root, ftpTaskConfig.localDirectory);
                }

                if (!Directory.Exists(rootPath))
                {
                    Console.WriteLine($"Could not found directory:{rootPath}");
                    return;
                }

                var localFiles = Directory.GetFiles(rootPath, ftpTaskConfig.searchPattern, new EnumerationOptions()
                {
                    MatchCasing = MatchCasing.CaseInsensitive,
                    RecurseSubdirectories = ftpTaskConfig.includeSubDirectories,
                });
                if (!localFiles.Any())
                {
                    Console.WriteLine($"Cound not found any files in {rootPath}");
                    return;
                }

                Console.WriteLine("Local Objects Begin");
                foreach (var localFile in localFiles)
                {
                    var fileInfo = new FileInfo(localFile);
                    Console.WriteLine($"FullName:{fileInfo.FullName},ModifiedTime:{fileInfo.LastWriteTime},Length:{fileInfo.Length}");
                }
                Console.WriteLine("Local Objects End");

                if (ftpTaskConfig.filters != null && ftpTaskConfig.filters.Any())
                {
                    localFiles = localFiles.Where(x =>
                    {
                        foreach (var filter in ftpTaskConfig.filters)
                        {
                            if (x.Contains(filter))
                            {
                                return true;
                            }
                        }
                        return false;
                    }).ToArray();
                }


                ftpTaskConfig.remoteDirectory = string.Format(ftpTaskConfig.remoteDirectory, hostName);


                var remoteFileListDict = ftpClient.GetListing(ftpTaskConfig.remoteDirectory,
                    FtpListOption.Recursive
                    ).ToDictionary(x => x.FullName);



                Console.WriteLine("Ftp objects Begin");
                foreach (var item in remoteFileListDict)
                {
                    Console.WriteLine($"FullName:{item.Key},ModifiedTime:{item.Value.Modified},Length:{item.Value.Size}");
                }
                Console.WriteLine("Ftp objects End");

                int uploadedCount = 0;
                int skippedCount = 0;
                int overidedCount = 0;
                foreach (var fileGroup in localFiles.GroupBy(x => Path.GetDirectoryName(x)))
                {


                    var dir = fileGroup.Key;

                    foreach (var localFilePath in fileGroup)
                    {
                        string remoteFilePath = CaculateRemoteFilePath(ftpTaskConfig.remoteDirectory, rootPath, localFilePath);
                        remoteFilePath = remoteFilePath.Replace("\\", "/");
                        remoteFilePath = remoteFilePath.Replace("/./", "/");
                        if (remoteFileListDict.TryGetValue(remoteFilePath, out var remoteFileInfo))
                        {
                            var localFileInfo = new FileInfo(localFilePath);
                            if (CompareFileInfo(remoteFileInfo, localFileInfo)
                                )
                            {
                                Console.WriteLine($"SkipFile:LocalFilePath:{localFileInfo.FullName} Size:{localFileInfo.Length} CreateDate:{localFileInfo.LastWriteTime} " +
                                    $" RemoteFilePath:{remoteFileInfo.FullName} Size:{remoteFileInfo.Size} CreateDate:{remoteFileInfo.Modified}");
                                continue;
                            }
                        }

                        FtpStatus ftpStatus = FtpStatus.Failed;

                        do
                        {
                            try
                            {
                                ftpStatus = ftpClient.UploadFile(localFilePath,
                                    remoteFilePath,
                                    FtpRemoteExists.Overwrite,
                                    true,
                                    FtpVerify.None,
                                    ShowProgress);
                            }
                            catch (System.TimeoutException ex)
                            {
                                Console.WriteLine(ex.ToString());
                                Thread.Sleep(1000);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                                Thread.Sleep(1000);
                            }
                        } while (ftpStatus == FtpStatus.Failed);




                        if (ftpStatus == FtpStatus.Success)
                        {
                            uploadedCount++;
                            Console.WriteLine($"Upload success:LocalPath:{localFilePath} RemotePath:{remoteFilePath}");
                        }
                        else if (ftpStatus == FtpStatus.Skipped)
                        {
                            skippedCount++;
                            Console.WriteLine($"Skip :LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
                            var remoteInfo = remoteFileInfo == null ? ftpClient.GetObjectInfo(remoteFilePath, true) : remoteFileInfo;
                            var localFileInfo = new FileInfo(localFilePath);
                            if (!CompareFileInfo(remoteInfo, localFileInfo))
                            {
                                ftpClient.DeleteFile(remoteFilePath);
                                ftpStatus = FtpStatus.Failed;
                                do
                                {
                                    try
                                    {
                                        ftpStatus = ftpClient.UploadFile(localFilePath,
                                             remoteFilePath,
                                             FtpRemoteExists.Overwrite,
                                             true,
                                             FtpVerify.None,
                                             OverideShowProgress);
                                        Console.WriteLine(ftpStatus);
                                    }
                                    catch (System.TimeoutException ex)
                                    {
                                        Console.WriteLine(ex.ToString());
                                        Thread.Sleep(1000);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.ToString());
                                        Thread.Sleep(1000);
                                    }

                                } while (ftpStatus == FtpStatus.Failed);

                                overidedCount++;
                                Console.WriteLine($"Override :LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
                            }
                        }
                        var lastWriteTime = File.GetLastWriteTime(localFilePath);
                        ftpClient.SetModifiedTime(remoteFilePath, lastWriteTime.ToUniversalTime());
                        Console.WriteLine($"ModifiedTime:{lastWriteTime}");
                    }
                }
                Console.WriteLine($"uploadedCount:{uploadedCount} skippedCount:{skippedCount} overidedCount:{overidedCount}");

            }
        }

        private static bool CompareFileInfo(FtpListItem remoteFileInfo, FileInfo localFileInfo)
        {
            Console.WriteLine($"LocalFileInfo{localFileInfo.FullName} RemoteFileInfo:{remoteFileInfo.FullName} {localFileInfo.LastWriteTimeUtc.Date} VS {remoteFileInfo.RawModified.Date} ");

            bool compareFileSize = remoteFileInfo.Size == localFileInfo.Length;
            bool compareDateTime = false;
            if (localFileInfo.LastWriteTimeUtc < DateTime.Today.Date.AddDays(-90))
            {
                compareDateTime = localFileInfo.LastWriteTimeUtc.Date == remoteFileInfo.RawModified.Date;
            }
            else
            {
                compareDateTime = false;
            }
            return compareFileSize && compareDateTime;
        }

        private static string CaculateRemoteFilePath(string remoteRootDir, string rootPath, string? localFilePath)
        {
            var parentDir = Path.GetDirectoryName(localFilePath);
            var relativePath = Path.GetRelativePath(rootPath, parentDir);
            var fileName = Path.GetFileName(localFilePath);
            var remoteFilePath = Path.Combine(remoteRootDir, relativePath, fileName);
            return remoteFilePath;
        }

        private void ShowProgress(FtpProgress ftpProgress)
        {
            if (ftpProgress.Progress == 100)
            {
                Console.WriteLine($"LocalFile:{ftpProgress.LocalPath} RemoteFile:{ftpProgress.RemotePath} Progress:{ftpProgress.Progress}");
            }
        }

        private void OverideShowProgress(FtpProgress ftpProgress)
        {
            if (ftpProgress.Progress == 100)
            {
                Console.WriteLine($"Overide: LocalFile:{ftpProgress.LocalPath} RemoteFile:{ftpProgress.RemotePath}  Progress:{ftpProgress.Progress}");
            }
        }



    }
}
