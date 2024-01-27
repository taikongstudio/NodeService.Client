using Confluent.Kafka;
using FluentFTP;
using JobsWorkerNode.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace JobsWorkerNode.Workers
{
    public class JobWorker : BackgroundService
    {
        private readonly ILogger<JobWorker> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private IScheduler _scheduler;
        private string _configPath;
        private string _watchPath;
        private FileSystemWatcher _fileSystemWatcher;
        private List<JobScheduleTask> _tasks;
        private int _hashCode;
        private Channel<FileSystemEventInfo> _channel;
        private string _localServerConfigPath;

        private FtpServerConfig _currentServerConfig;

        public JobWorker(ILogger<JobWorker> logger, ISchedulerFactory schedulerFactory)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _tasks = new List<JobScheduleTask>();
            _channel = Channel.CreateUnbounded<FileSystemEventInfo>();
        }

        private void MyLog(string message)
        {
            _logger.LogInformation(message);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _localServerConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "server.bat");
                if (!FtpServerConfig.TryLoadServerConfig(_localServerConfigPath, _logger, out FtpServerConfig ftpServerConfig))
                {
                    MyLog("Load server config fail");
                    return;
                }

                if (ftpServerConfig == null)
                {
                    MyLog("Load server config fail");
                    return;
                }

                _currentServerConfig = ftpServerConfig;

                _watchPath = Path.Combine(AppContext.BaseDirectory, "config");
                _configPath = Path.Combine(AppContext.BaseDirectory, "config", ftpServerConfig.version, "config.bat");

                _scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
                await _scheduler.Start(stoppingToken);
                if (!Directory.Exists(_watchPath))
                {
                    Directory.CreateDirectory(_watchPath);
                }

                await Task.Run(ReinitFileSystemWatcher);

                await TryLoadTasksFromConfigAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.ToString());
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    long index = 0;
                    FileSystemEventInfo lastFileSystemEventInfo = null;
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        while (_channel.Reader.TryRead(out var eventInfo))
                        {
                            if (lastFileSystemEventInfo != null
                                &&
                                lastFileSystemEventInfo.ChangeTypes == WatcherChangeTypes.Created
                                &&
                                eventInfo.ChangeTypes == WatcherChangeTypes.Changed)
                            {

                            }
                            await ProcessEventInfo(eventInfo);
                            await Task.Delay(TimeSpan.FromSeconds(5));
                            lastFileSystemEventInfo = eventInfo;
                        }
                        if (index % 60 == 0)
                        {
                            await TryLoadTasksFromConfigAsync();
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }
                        if (index > 1000)
                        {
                            index = 0;
                            _logger.LogInformation("Reset index");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        index++;
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex.ToString());
                }
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            Environment.Exit(0);
        }

        private async Task ProcessEventInfo(FileSystemEventInfo eventInfo)
        {
            switch (eventInfo.ChangeTypes)
            {
                case WatcherChangeTypes.Created:
                    try
                    {
                        _hashCode = 0;
                        _logger.LogInformation($"Created:{eventInfo.FullPath}");
                        await TryLoadTasksFromConfigAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }
                    break;
                case WatcherChangeTypes.Deleted:
                    try
                    {
                        _hashCode = 0;
                        _logger.LogInformation($"Deleted:{eventInfo.FullPath}");
                        await ClearTasksAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }
                    break;
                case WatcherChangeTypes.Changed:
                    try
                    {
                        _hashCode = 0;
                        _logger.LogInformation($"Changed:{eventInfo.FullPath}");
                        await TryLoadTasksFromConfigAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                    }
                    break;
                case WatcherChangeTypes.Renamed:
                    break;
                case WatcherChangeTypes.All:
                    break;
                default:
                    break;
            }
        }

        private bool TryInitFileSystemWatcher()
        {
            try
            {
                if (_fileSystemWatcher != null)
                {
                    _fileSystemWatcher.EnableRaisingEvents = false;
                    _fileSystemWatcher.Created -= _fileSystemWatcher_Created;
                    _fileSystemWatcher.Error -= _fileSystemWatcher_Error;
                    _fileSystemWatcher.Deleted -= _fileSystemWatcher_Deleted;
                    _fileSystemWatcher.Renamed -= _fileSystemWatcher_Renamed;
                    _fileSystemWatcher.Changed -= _fileSystemWatcher_Changed;
                    _fileSystemWatcher.Dispose();
                }
                _fileSystemWatcher = new FileSystemWatcher(_watchPath);

                _fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
    | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.LastAccess
    | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.Security;
                _fileSystemWatcher.IncludeSubdirectories = true;
                _fileSystemWatcher.Created += _fileSystemWatcher_Created;
                _fileSystemWatcher.Error += _fileSystemWatcher_Error;
                _fileSystemWatcher.Deleted += _fileSystemWatcher_Deleted;
                _fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
                _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
                _fileSystemWatcher.EnableRaisingEvents = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        private async void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            await _channel.Writer.WriteAsync(new FileSystemEventInfo()
            {
                ChangeTypes = WatcherChangeTypes.Changed,
                FullPath = e.FullPath,
            });
        }

        private void _fileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {

        }

        private async void _fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            await _channel.Writer.WriteAsync(new FileSystemEventInfo()
            {
                ChangeTypes = WatcherChangeTypes.Deleted,
                FullPath = e.FullPath,
            });

        }

        private void _fileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException().ToString());
            Task.Run(ReinitFileSystemWatcher);
        }

        private async void ReinitFileSystemWatcher()
        {
            while (true)
            {
                if (TryInitFileSystemWatcher())
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }

        }

        private async void _fileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            await _channel.Writer.WriteAsync(new FileSystemEventInfo()
            {
                ChangeTypes = WatcherChangeTypes.Created,
                FullPath = e.FullPath,
            });

        }

        private List<MachineInfo> LoadMachineInfoList()
        {
            List<MachineInfo> machineInfoList = new List<MachineInfo>();
            try
            {

                var serverConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "server.bat");
                if (!FtpServerConfig.TryLoadServerConfig(_localServerConfigPath, _logger, out FtpServerConfig ftpServerConfig))
                {
                    MyLog("Load server config fail");
                    return machineInfoList;
                }
                _currentServerConfig = ftpServerConfig;
                using (var ftpClient = new FtpClient(ftpServerConfig.server, ftpServerConfig.username, ftpServerConfig.password))
                {
                    ftpClient.AutoConnect();
                    using var memoryStream = new MemoryStream();
                    if (!ftpClient.DownloadStream(memoryStream, "/JobsWorkerDeamonServiceConfig/machines.csv"))
                    {
                        _hashCode = 0;
                        return machineInfoList;
                    }
                    else
                    {
                        MyLog("Download machines.csv data sucess");
                        memoryStream.Position = 0;
                        Dictionary<string, string> mapping = new Dictionary<string, string>()
                        {
                            //厂区,测试业务,实验室区域,电脑所属实验室,电脑名称,登录账号,状态,计划完成时间,实际完成时间,备注
                            {"厂区","factory_name" },
                            {"测试业务","test_info" },
                            {"实验室区域","lab_area" },
                            {"电脑所属实验室","lab_name" },
                            {"电脑名称","computer_name" },
                            {"登录账号","login_name" }
                        };

                        using (var streamReader = new StreamReader(memoryStream))
                        {
                            Dictionary<string, int> headerDict = new Dictionary<string, int>();
                            while (!streamReader.EndOfStream)
                            {
                                var line = streamReader.ReadLine();
                                if (headerDict.Count == 0)
                                {
                                    int index = 0;
                                    foreach (var item in line.Split(',', StringSplitOptions.None))
                                    {
                                        if (!mapping.TryGetValue(item, out var columnName))
                                        {
                                            continue;
                                        }
                                        headerDict.Add(mapping[item], index);
                                        index++;
                                    }
                                }
                                else
                                {
                                    var segments = line.Split(',');
                                    MachineInfo machineinfo = new MachineInfo();
                                    machineinfo.factory_name = segments[headerDict[nameof(machineinfo.factory_name)]];
                                    machineinfo.test_info = segments[headerDict[nameof(machineinfo.test_info)]];
                                    machineinfo.lab_area = segments[headerDict[nameof(machineinfo.lab_area)]];
                                    machineinfo.lab_name = segments[headerDict[nameof(machineinfo.lab_name)]];
                                    machineinfo.computer_name = segments[headerDict[nameof(machineinfo.computer_name)]];
                                    machineinfo.login_name = segments[headerDict[nameof(machineinfo.login_name)]];
                                    machineinfo.host_name = "";
                                    machineInfoList.Add(machineinfo);
                                }
                            }
                        }
                    }
                    MyLog($"Read machines:{machineInfoList.Count}");
                }
            }
            catch (Exception ex)
            {
                _hashCode = 0;
                MyLog(ex.ToString());
            }

            return machineInfoList;
        }

        private async Task TryLoadTasksFromConfigAsync()
        {
            try
            {
                var hostName = Dns.GetHostName();

                var loadConfigResult = TryLoadJsonConfig(out var jobScheduleConfigs);

                if (loadConfigResult == LoadConfigResult.LoadFail)
                {
                    _logger.LogWarning($"Could not found config:{_configPath}");
                    return;
                }

                if (loadConfigResult == LoadConfigResult.NotChanged)
                {
                    _logger.LogWarning($"Not changed:{_configPath}");
                    return;
                }

                if (jobScheduleConfigs.Any())
                {
                    await ClearTasksAsync();
                }
                var machineList = LoadMachineInfoList();
                foreach (var config in jobScheduleConfigs)
                {
                    try
                    {
                        if (!config.isEnabled)
                        {
                            _logger.LogInformation("Skip schedule reason:disabled");
                            continue;
                        }
                        if (!FilterHostName(config))
                        {
                            _logger.LogInformation("Skip schedule reason:host filter");
                            continue;
                        }
                        if (!string.IsNullOrEmpty(config.factory_name))
                        {
                            var machine = machineList.Where(x => x.computer_name != null)
                                .FirstOrDefault(x => x.computer_name.Equals(hostName, StringComparison.OrdinalIgnoreCase));
                            if (machine == null || machine.factory_name != config.factory_name)
                            {
                                _logger.LogInformation($"Skip schedule reason:hostname:{hostName} factory_name:{config.factory_name},machine:{machine?.computer_name},factory_name:{machine?.factory_name}");
                                continue;
                            }
                        }
                        var task = new JobScheduleTask(_logger, _scheduler);
                        await task.ScheduleAsync(config);
                        lock (_tasks)
                        {
                            _tasks.Add(task);
                        }
                        _logger.LogInformation($"Schedule Task:{JsonSerializer.Serialize(config)}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{config?.jobName}:{ex}");
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {

            }

        }

        private bool FilterHostName(JobScheduleConfig config)
        {
            try
            {
                if (config.hostNameFilters != null && config.hostNameFilters.Any())
                {
                    var dns = Dns.GetHostName();
                    if (config.hostNameFilterType == "include" && !config.hostNameFilters.Any(x => x.Equals(dns, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                    if (config.hostNameFilterType == "exclude" && config.hostNameFilters.Any(x => x.Equals(dns, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                    if (config.factory_name != null)
                    {

                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        private async Task ClearTasksAsync()
        {
            JobScheduleTask[] tasks = Array.Empty<JobScheduleTask>();
            lock (_tasks)
            {
                tasks = _tasks.ToArray();
                _tasks.Clear();
            }

            foreach (var task in tasks)
            {
                await task.CancelAsync();
            }
            _logger.LogInformation($"Clear Tasks:{tasks.Length}");
        }

        private LoadConfigResult TryLoadJsonConfig(out JobScheduleConfig[] jobScheduleConfigs)
        {
            jobScheduleConfigs = Array.Empty<JobScheduleConfig>();
            try
            {
                if (!File.Exists(_configPath))
                {
                    return LoadConfigResult.LoadFail;
                }
                string json = null;
                PathLocker.Lock(_configPath, () =>
                {
                    json = File.ReadAllText(_configPath);
                });
                jobScheduleConfigs = JsonSerializer.Deserialize<JobScheduleConfig[]>(json);
                if (_hashCode == json.GetHashCode())
                {
                    return LoadConfigResult.NotChanged;
                }
                _logger.LogInformation("Load config success:");
                _hashCode = json.GetHashCode();
                return LoadConfigResult.Changed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return LoadConfigResult.LoadFail;
            }

        }

    }
}
