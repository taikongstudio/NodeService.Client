using FluentFTP;
using FluentFTP.Helpers;
using JobsWorkerNodeService;
using JobsWorkerNodeService.Helpers;
using JobsWorkerWebService.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs
{
    public class UpdateStatusJob : JobBase
    {

        private void MyLog(string message)
        {
            Logger.LogInformation(message);
        }

        private bool TryGetHostAddressStrings(out string[]? hostAddressStrings)
        {
            hostAddressStrings = null;
            try
            {
                var hostAddresses = Dns.GetHostAddresses(Dns.GetHostName());
                hostAddressStrings = hostAddresses.Select(x => x.ToString()).ToArray();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            return false;
        }

        public override async Task Execute(IJobExecutionContext context)
        {
            try
            {
                Logger.LogInformation($"Begin update");
                var randomSeconds = Debugger.IsAttached ? 0 : Random.Shared.Next(60 * 10, 60 * 29);
                await Task.Delay(TimeSpan.FromSeconds(randomSeconds + Random.Shared.NextDouble()));

                MySqlConfig mySqlConfig = new MySqlConfig();
                mySqlConfig.mysql_host = Arguments["mysql_host"];
                mySqlConfig.mysql_userid = Arguments["mysql_userid"];
                mySqlConfig.mysql_password = Arguments["mysql_password"];
                mySqlConfig.mysql_database = Arguments["mysql_database"];

                var factory_name = Arguments["factory_name"];
                var server = Arguments["server"];
                var username = Arguments["username"];
                var password = Arguments["password"];
                int configExpireMinutes = ParseConfigExipireMinutes();

                using var ftpClient = new FtpClient(server, username, password);
                ftpClient.AutoConnect();
                var processList = CollectProcessList(ftpClient);

                var hostName = Dns.GetHostName();
                var remotePath = Path.Combine("/JobsWorkerDaemonService", hostName, "machineinfo.json");

                if (!TryDownloadMachineConfigFromFtpServer(ftpClient, remotePath, out DeviceInfo? current_machine_info)
                    ||
                    current_machine_info == null
                    ||
                    !DateTime.TryParse(current_machine_info.UpdateTime, out var updateTime)
                    ||
                    CompareDateTimes(updateTime, DateTime.Now, TimeSpan.FromMinutes(configExpireMinutes))
                    )
                {
                    current_machine_info = EnsureMachineInfo(mySqlConfig, factory_name, hostName);

                }

                current_machine_info.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                current_machine_info.ip_addresses = GetIPAddressesString();
                current_machine_info.install_status = true;
                current_machine_info.update_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                current_machine_info.version = Constants.Version;
                current_machine_info.usages = AnalysisUsages(current_machine_info.usages, processList);

                var jsonString = JsonSerializer.Serialize(current_machine_info);

                Logger.LogInformation($"Updated:{jsonString}");

                UploadMachineInfoToFtp(ftpClient, remotePath, current_machine_info);


                await UploadAllMachineInfoToFtpAsync(ftpClient, mySqlConfig);

                //await SqlHelper.InsertOrUpdate(mySqlConfig, new MachineInfo[] { current_machine_info }, MyLog);
                //this.Logger.LogInformation($"Updated:{JsonSerializer.Serialize(current_machine_info)}");

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

        private string AnalysisUsages(string? usages, List<ProcessStatInfo> processStatInfos)
        {
            try
            {
                var server = Arguments["server"];
                var username = Arguments["username"];
                var password = Arguments["password"];
                usages = string.IsNullOrEmpty(usages) ? string.Empty : usages;
                var processStatToolPath = Path.Combine(AppContext.BaseDirectory, "plugins", "ProcessStatTool", "default", "ProcessStatTool.exe");
                var tempDir = Path.Combine(AppContext.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                var inputFilePath = Path.Combine("JobsWorkerDaemonService", Dns.GetHostName(), "processlist");
                var outputFilePath = Path.Combine(tempDir, Guid.NewGuid().ToString("N"));
                string args = $"--host={server} --username={username} --password={password} --action=usages --input=\"{inputFilePath}\" --output=\"{outputFilePath}\"";
                Logger.LogInformation($"Args:{args}");
                using var process = Process.Start(processStatToolPath, args);
                process.WaitForExit();
                var str = File.ReadAllText(outputFilePath);
                var processStatAnalysisResult = JsonSerializer.Deserialize<ProcessStatAnalysisResult>(str);
                Logger.LogInformation($"ProcessStatAnalysisResult:{str}");
                if (processStatAnalysisResult != null)
                {
                    var usagesHashSet = usages.Replace("，", ",").Replace(".", ",").Replace("、", ",").Split(",").ToHashSet();
                    foreach (var entry in processStatAnalysisResult.Entries)
                    {
                        if (entry.Exists && !usagesHashSet.Contains(entry.Name))
                        {
                            usagesHashSet.Add(entry.Name);
                        }
                    }
                    var list = usagesHashSet.ToList();
                    list.Sort();
                    usages = string.Join(",", list);
                }
                if (File.Exists(outputFilePath))
                {
                    File.Delete(outputFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            return usages;
        }

        private DeviceInfo EnsureMachineInfo(MySqlConfig mySqlConfig, string factory_name, string hostName)
        {
            DeviceInfo? current_machine_info;
            var machineList = SqlHelper.GetMachineInfoList(mySqlConfig, Logger);
            current_machine_info = machineList.FirstOrDefault(x =>
                string.Equals(x.computer_name,
                hostName,
                StringComparison.OrdinalIgnoreCase));

            if (current_machine_info == null)
            {
                Logger.LogInformation($"Could not found machine info:{hostName}");
                current_machine_info = new DeviceInfo();
                current_machine_info.computer_name = hostName;
                current_machine_info.factory_name = factory_name;
                current_machine_info.install_status = true;
                current_machine_info.test_info = string.Empty;
                current_machine_info.lab_area = string.Empty;
                current_machine_info.lab_name = string.Empty;
                current_machine_info.login_name = string.Empty;
                current_machine_info.host_name = string.Empty;
            }
            else
            {
                Logger.LogInformation($"Found machine info:{hostName}=>{JsonSerializer.Serialize(current_machine_info)}");
            }

            return current_machine_info;
        }

        private int ParseConfigExipireMinutes()
        {
            try
            {
                if (Arguments.TryGetValue("configExpireMinutes", out var configExpireMinutesStr)
                    && int.TryParse(configExpireMinutesStr, out int configExpireMinutes))
                {
                    return configExpireMinutes;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            return 60;
        }

        private string GetIPAddressesString()
        {
            string ipAddreses = string.Empty;
            if (TryGetHostAddressStrings(out var hostAddressStrings))
            {
                ipAddreses = string.Join(",", hostAddressStrings);
            }

            return ipAddreses;
        }

        private bool CompareDateTimes(DateTime dateTime1, DateTime dateTime2, TimeSpan timeSpan)
        {
            return dateTime2 - dateTime1 > timeSpan;
        }


        private bool TryDownloadMachineConfigFromFtpServer(FtpClient ftpClient, string remotePath, out DeviceInfo? machineInfo)
        {

            machineInfo = null;

            try
            {
                if (ftpClient.FileExists(remotePath))
                {
                    return DownloadMachineInfo(ftpClient, remotePath, out machineInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            return false;
        }

        private void UploadMachineInfoToFtp(FtpClient ftpClient, string remotePath, DeviceInfo? current_machine_info)
        {
            using (var memoryStream = new MemoryStream())
            {
                JsonSerializer.Serialize(memoryStream, current_machine_info);
                memoryStream.Position = 0;
                var uploadStatus = ftpClient.UploadStream(memoryStream, remotePath, FtpRemoteExists.Overwrite, true);
                Logger.LogInformation($"Upload:{remotePath} {uploadStatus}");
            }
        }

        private class LockFileContent
        {
            public string HostName { get; set; }
            public string DateTime { get; set; }

            public string IPAddresses { get; set; }
        }


        private async Task UploadAllMachineInfoToFtpAsync(FtpClient ftpClient, MySqlConfig mySqlConfig)
        {

            var lockSeconds = int.Parse(Arguments["lockSeconds"]);
            var lockFilePath = "/JobsWorkerDaemonService/mysql.lock";
            var lockFileInfo = ftpClient.GetObjectInfo(lockFilePath);
            if (lockFileInfo != null)
            {
                if (DateTime.Now.ToUniversalTime() - lockFileInfo.Modified < TimeSpan.FromSeconds(lockSeconds))
                {
                    Logger.LogInformation("Lock exits, Exit");
                    return;
                }
                using var memoryStream = new MemoryStream();
                if (ftpClient.DownloadStream(memoryStream, lockFilePath))
                {
                    memoryStream.Position = 0;
                    var lockFileContent = JsonSerializer.Deserialize<LockFileContent>(memoryStream);
                    if (DateTime.TryParse(lockFileContent.DateTime, out DateTime dateTime)
                        &&
                        DateTime.Now - dateTime < TimeSpan.FromSeconds(lockSeconds)
                        )
                    {
                        Logger.LogInformation("Lock exits, Exit");
                        return;
                    }
                }

            }
            WriteCurrentLockContent(ftpClient, lockFilePath);
            List<DeviceInfo> machineInfoList = new List<DeviceInfo>();
            DownloadMachineListFromFtp(ftpClient, machineInfoList);
            //todo
        }

        private void DeleteLockFile(FtpClient ftpClient, string lockFilePath)
        {
            if (ftpClient.FileExists(lockFilePath))
            {
                ftpClient.DeleteFile(lockFilePath);
                Logger.LogInformation($"Delete Lock File");
            }
        }

        private void DownloadMachineListFromFtp(FtpClient ftpClient, List<DeviceInfo> machineInfoList)
        {
            foreach (var machineDir in ftpClient.GetListing("JobsWorkerDaemonService"))
            {
                var remotePath = Path.Combine("/JobsWorkerDaemonService", machineDir.Name, "machineinfo.json");
                if (!ftpClient.FileExists(remotePath))
                {
                    continue;
                }
                if (DownloadMachineInfo(ftpClient, remotePath, out DeviceInfo machineInfo))
                {
                    machineInfoList.Add(machineInfo);
                }
            }
        }

        private void WriteCurrentLockContent(FtpClient ftpClient, string lockFilePath)
        {
            using (var memoryStream = new MemoryStream())
            {
                LockFileContent lockFileContent = new LockFileContent()
                {
                    DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    HostName = Dns.GetHostName(),
                    IPAddresses = GetIPAddressesString()
                };
                JsonSerializer.Serialize(memoryStream, lockFileContent);
                memoryStream.Position = 0;
                ftpClient.UploadStream(memoryStream, lockFilePath, FtpRemoteExists.Overwrite, true);
            }
        }

        private bool DownloadMachineInfo(FtpClient ftpClient, string remotePath, out DeviceInfo machineInfo)
        {
            machineInfo = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Position = 0;
                if (ftpClient.DownloadStream(memoryStream, remotePath, 0, ShowDownloadProgress))
                {
                    memoryStream.Position = 0;
                    machineInfo = JsonSerializer.Deserialize<DeviceInfo>(memoryStream);
                    Logger.LogInformation($"Downloaded:{remotePath} {JsonSerializer.Serialize(machineInfo)}");
                    return true;
                }
                else
                {
                    Logger.LogInformation($"Download fails:{remotePath}");
                    return false;
                }
            }
        }

        private List<ProcessStatInfo> CollectProcessList(FtpClient ftpClient)
        {
            List<ProcessStatInfo> processStatList = new List<ProcessStatInfo>();
            try
            {

                var server = Arguments["server"];
                var username = Arguments["username"];
                var password = Arguments["password"];


                var processes = Process.GetProcesses();
                Dictionary<string, int> errors = new Dictionary<string, int>();
                foreach (var process in processes)
                {
                    try
                    {
                        ProcessStatInfo processStatInfo = new ProcessStatInfo()
                        {
                            FileName = process.ProcessName,
                            ProcessName = process.MainModule.FileName,
                            Id = process.Id,
                            Responding = process.Responding,
                            StartTime = process.StartTime.ToString("yyyy-MM-dd hh:MM:ss"),
                            ExitTime = process.HasExited ? process.ExitTime.ToString("yyyy-MM-dd hh:MM:ss") : null,
                            VirtualMemorySize64 = process.VirtualMemorySize64,
                            PagedMemorySize64 = process.PagedMemorySize64,
                            NonpagedSystemMemorySize64 = process.NonpagedSystemMemorySize64,
                            PagedSystemMemorySize64 = process.PagedSystemMemorySize64,
                            HandleCount = process.HandleCount,
                        };
                        processStatList.Add(processStatInfo);
                    }
                    catch (Exception ex)
                    {
                        var errorString = ex.ToString();
                        if (!errors.ContainsKey(errorString))
                        {
                            errors.Add(errorString, 1);
                        }
                        else
                        {
                            errors[errorString]++;
                        }

                    }
                }

                foreach (var item in errors)
                {
                    Logger.LogError($"Count:{item.Value} Exception:{item.Key}");
                }

                var hostName = Dns.GetHostName();
                var remotePathDir = Path.Combine("/JobsWorkerDaemonService", hostName, "processlist");

                using (var memoryStream = new MemoryStream())
                {
                    JsonSerializer.Serialize(memoryStream, processStatList);
                    memoryStream.Position = 0;
                    var path = Path.Combine(remotePathDir, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm")}.json");
                    ftpClient.UploadStream(memoryStream, path, FtpRemoteExists.Overwrite, true, ShowUploadProgress);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            return processStatList;
        }

        private void ShowUploadProgress(FtpProgress ftpProgress)
        {
            if (ftpProgress.Progress == 100)
            {
                Logger.LogInformation($"Remote path:{ftpProgress.RemotePath},Size:{ftpProgress.TransferredBytes},Progress:{ftpProgress.Progress}");
            }
        }

        private void ShowDownloadProgress(FtpProgress ftpProgress)
        {
            if (ftpProgress.Progress == 100)
            {
                Logger.LogInformation($"Local Path:{ftpProgress.LocalPath} Remote path:{ftpProgress.RemotePath},Size:{ftpProgress.TransferredBytes},Progress:{ftpProgress.Progress}");
            }
        }

    }
}
