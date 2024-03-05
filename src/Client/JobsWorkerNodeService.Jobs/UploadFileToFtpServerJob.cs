using FluentFTP;
using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.Models;
using JobsWorkerNodeService.Jobs.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs
{
    public class UploadFileToFtpServerJob : JobBase
    {
        public class FtpTaskExecutor
        {
            public FtpUploadConfigModel FtpUploadConfig { get; set; }


            public ILogger Logger { get; set; }

            public FtpTaskExecutor(
                FtpUploadConfigModel ftpTaskConfig,
                ILogger logger)
            {
                this.FtpUploadConfig = ftpTaskConfig;
                this.Logger = logger;
            }



            public async Task RunAsync()
            {
                await this.ExecuteAsync(this.FtpUploadConfig);
            }

            private async Task ExecuteAsync(FtpUploadConfigModel ftpTaskConfig)
            {
                try
                {
                    MyFtpProgress myFtpProgress = new MyFtpProgress(this.Logger);
                    using (var ftpClient = new AsyncFtpClient(this.FtpUploadConfig.FtpConfig.Host,
                        this.FtpUploadConfig.FtpConfig.Username,
                        this.FtpUploadConfig.FtpConfig.Password,
                        this.FtpUploadConfig.FtpConfig.Port, new FtpConfig()
                        {
                            ConnectTimeout = this.FtpUploadConfig.FtpConfig.ConnectTimeout,
                            ReadTimeout = this.FtpUploadConfig.FtpConfig.ReadTimeout,
                            DataConnectionReadTimeout = this.FtpUploadConfig.FtpConfig.DataConnectionReadTimeout,
                            DataConnectionConnectTimeout = this.FtpUploadConfig.FtpConfig.DataConnectionConnectTimeout,
                        }))
                    {
                        await ftpClient.AutoConnect();
                        this.Logger.LogInformation($"Execute config on host {this.FtpUploadConfig.FtpConfig.Host} port {this.FtpUploadConfig.FtpConfig.Port}" +
                            $" username {this.FtpUploadConfig.FtpConfig.Username} password {this.FtpUploadConfig.FtpConfig.Password}");


                        string rootPath = ftpTaskConfig.LocalDirectory;

                        var hostName = Dns.GetHostName();

                        if (ftpTaskConfig.IsLocalDirectoryUseMapping)
                        {
                            var localDirectoryConfig = this.FtpUploadConfig.LocalDirectoryMappingConfig;
                            var path = localDirectoryConfig.Entries.FirstOrDefault(x => x.Name == this.FtpUploadConfig.LocalDirectory).Value;
                            rootPath = Path.Combine(path);
                        }
                        if (!Directory.Exists(rootPath))
                        {
                            this.Logger.LogInformation($"Could not found directory:{rootPath}");
                            return;
                        }

                        var localFiles = Directory.GetFiles(rootPath, ftpTaskConfig.SearchPattern, new EnumerationOptions()
                        {
                            MatchCasing = MatchCasing.CaseInsensitive,
                            RecurseSubdirectories = ftpTaskConfig.IncludeSubDirectories,
                        });
                        if (!localFiles.Any())
                        {
                            Console.WriteLine($"Cound not found any files in {rootPath}");
                            return;
                        }

                        this.Logger.LogInformation("Enumerate local Objects Begin");
                        foreach (var localFile in localFiles)
                        {
                            var fileInfo = new FileInfo(localFile);
                            this.Logger.LogInformation($"FullName:{fileInfo.FullName},ModifiedTime:{fileInfo.LastWriteTime},Length:{fileInfo.Length}");
                        }
                        this.Logger.LogInformation("Enumerate local Objects End");

                        if (ftpTaskConfig.Filters != null && ftpTaskConfig.Filters.Any())
                        {
                            localFiles = localFiles.Where(x =>
                            {
                                foreach (var filter in ftpTaskConfig.Filters)
                                {
                                    if (x.Contains(filter.Value))
                                    {
                                        return true;
                                    }
                                }
                                return false;
                            }).ToArray();
                        }


                        ftpTaskConfig.RemoteDirectory = string.Format(ftpTaskConfig.RemoteDirectory, hostName);


                        var remoteFileListDict = (await ftpClient.GetListing(ftpTaskConfig.RemoteDirectory,
                            FtpListOption.Recursive
                            )).ToDictionary<FtpListItem, string>(x => x.FullName);



                        this.Logger.LogInformation("Enumerate Ftp objects Begin");
                        foreach (var item in remoteFileListDict)
                        {
                            this.Logger.LogInformation($"FullName:{item.Key},ModifiedTime:{item.Value.Modified},Length:{item.Value.Size}");
                        }
                        this.Logger.LogInformation("Enumerate Ftp objects End");

                        int uploadedCount = 0;
                        int skippedCount = 0;
                        int overidedCount = 0;
                        foreach (var fileGroup in localFiles.GroupBy<string, string>(x => Path.GetDirectoryName(x)))
                        {
                            foreach (var localFilePath in fileGroup)
                            {
                                string remoteFilePath = CaculateRemoteFilePath(ftpTaskConfig.RemoteDirectory, rootPath, localFilePath);
                                remoteFilePath = remoteFilePath.Replace("\\", "/");
                                remoteFilePath = remoteFilePath.Replace("/./", "/");
                                if (remoteFileListDict.TryGetValue(remoteFilePath, out var remoteFileInfo))
                                {
                                    var localFileInfo = new FileInfo(localFilePath);
                                    if (CompareFileInfo(remoteFileInfo, localFileInfo)
                                        )
                                    {
                                        this.Logger.LogInformation($"SkipFile:LocalFilePath:{localFileInfo.FullName} Size:{localFileInfo.Length} CreateDate:{localFileInfo.LastWriteTime} " +
                                            $" RemoteFilePath:{remoteFileInfo.FullName} Size:{remoteFileInfo.Size} CreateDate:{remoteFileInfo.Modified}");
                                        continue;
                                    }
                                }

                                FtpStatus ftpStatus = FtpStatus.Failed;

                                do
                                {
                                    try
                                    {
                                        ftpStatus = await ftpClient.UploadFile(localFilePath,
                                            remoteFilePath,
                                            FtpRemoteExists.Overwrite,
                                            true,
                                            FtpVerify.None,
                                            myFtpProgress);
                                    }
                                    catch (TimeoutException ex)
                                    {
                                        this.Logger.LogInformation(ex.ToString());
                                        await Task.Delay(TimeSpan.FromSeconds(30));
                                    }
                                    catch (Exception ex)
                                    {
                                        this.Logger.LogInformation(ex.ToString());
                                        await Task.Delay(TimeSpan.FromSeconds(30));
                                    }
                                } while (ftpStatus == FtpStatus.Failed);




                                if (ftpStatus == FtpStatus.Success)
                                {
                                    uploadedCount++;
                                    this.Logger.LogInformation($"Upload success:LocalPath:{localFilePath} RemotePath:{remoteFilePath}");
                                }
                                else if (ftpStatus == FtpStatus.Skipped)
                                {
                                    skippedCount++;
                                    this.Logger.LogInformation($"Skip :LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
                                    var remoteInfo = remoteFileInfo == null ? await ftpClient.GetObjectInfo(remoteFilePath, true) : remoteFileInfo;
                                    var localFileInfo = new FileInfo(localFilePath);
                                    if (!CompareFileInfo(remoteInfo, localFileInfo))
                                    {
                                        await ftpClient.DeleteFile(remoteFilePath);
                                        ftpStatus = FtpStatus.Failed;
                                        do
                                        {
                                            try
                                            {
                                                ftpStatus = await ftpClient.UploadFile(localFilePath,
                                                     remoteFilePath,
                                                     FtpRemoteExists.Overwrite,
                                                     true,
                                                     FtpVerify.None,
                                                     myFtpProgress);
                                                this.Logger.LogInformation($"{localFilePath} ftp status:{ftpStatus}");
                                            }
                                            catch (TimeoutException ex)
                                            {
                                                this.Logger.LogInformation(ex.ToString());
                                                await Task.Delay(TimeSpan.FromSeconds(30));
                                            }
                                            catch (Exception ex)
                                            {
                                                this.Logger.LogInformation(ex.ToString());
                                                await Task.Delay(TimeSpan.FromSeconds(30));
                                            }

                                        } while (ftpStatus == FtpStatus.Failed);

                                        overidedCount++;
                                        this.Logger.LogInformation($"Override :LocalPath:{localFilePath},RemotePath:{remoteFilePath}");
                                    }
                                }
                                var lastWriteTime = File.GetLastWriteTime(localFilePath);
                                await ftpClient.SetModifiedTime(remoteFilePath, lastWriteTime.ToUniversalTime());
                                this.Logger.LogInformation($"ModifiedTime:{lastWriteTime}");
                            }
                        }
                        this.Logger.LogInformation($"uploadedCount:{uploadedCount} skippedCount:{skippedCount} overidedCount:{overidedCount}");

                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.ToString());
                }

            }

            private bool CompareFileInfo(FtpListItem remoteFileInfo, FileInfo localFileInfo)
            {
                this.Logger.LogInformation($"LocalFileInfo{localFileInfo.FullName} RemoteFileInfo:{remoteFileInfo.FullName} {localFileInfo.LastWriteTimeUtc.Date} VS {remoteFileInfo.RawModified.Date} ");

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

        }




        public override async Task Execute(IJobExecutionContext context)
        {

            var ftpUploadConfigs = this.JobScheduleConfig.Options.ReadOptionArrayValues<FtpUploadConfigModel>("ftpUploadConfigNames", this.NodeConfigTemplate.FindFtpUploadConfig);
            foreach (var ftpUploadConfig in ftpUploadConfigs)
            {

                ftpUploadConfig.FtpConfig = this.NodeConfigTemplate.FindFtpConfig(ftpUploadConfig.FtpConfigForeignKey);
                ftpUploadConfig.LocalDirectoryMappingConfig = this.NodeConfigTemplate.FindLocalDirectoryMappingConfig(ftpUploadConfig.LocalDirectoryMappingConfigForeignKey);
                FtpTaskExecutor ftpTaskExecutor = new FtpTaskExecutor(ftpUploadConfig, this.Logger);
                await ftpTaskExecutor.RunAsync();
            }
        }

    }
}
