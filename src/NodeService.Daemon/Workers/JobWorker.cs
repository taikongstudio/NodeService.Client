using NodeService.Daemon;
using NodeService.Daemon.Models;
using Quartz;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace NodeService.Daemon.Workers
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
                _logger.LogInformation($"Start {nameof(JobWorker)}");
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
                _watchPath = Path.Combine(AppContext.BaseDirectory, "config");
                _configPath = Path.Combine(AppContext.BaseDirectory, "config", ftpServerConfig.version, "config.bat");

                _scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
                await _scheduler.Start(stoppingToken);
                if (!Directory.Exists(_watchPath))
                {
                    Directory.CreateDirectory(_watchPath);
                }
                _fileSystemWatcher = new FileSystemWatcher(_watchPath);
                _fileSystemWatcher.IncludeSubdirectories = true;
                _fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
    | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.LastAccess
    | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.Security;
                _fileSystemWatcher.Created += _fileSystemWatcher_Created;
                _fileSystemWatcher.Error += _fileSystemWatcher_Error;
                _fileSystemWatcher.Deleted += _fileSystemWatcher_Deleted;
                _fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
                _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
                _fileSystemWatcher.EnableRaisingEvents = true;

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
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var eventInfo = await _channel.Reader.ReadAsync(stoppingToken);
                        await Task.Delay(1000);
                        switch (eventInfo.ChangeTypes)
                        {
                            case WatcherChangeTypes.Created:
                                try
                                {
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

        }

        private async void _fileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            await _channel.Writer.WriteAsync(new FileSystemEventInfo()
            {
                ChangeTypes = WatcherChangeTypes.Created,
                FullPath = e.FullPath,
            });

        }

        private async Task TryLoadTasksFromConfigAsync()
        {
            try
            {
                var loadConfigResult = TryLoadJsonConfig(out var jobScheduleConfigs);

                if (loadConfigResult == LoadConfigResult.LoadFail)
                {
                    _logger.LogWarning($"Could not found config:{_configPath}");
                    return;
                }

                if (loadConfigResult == LoadConfigResult.NotChanged)
                {
                    return;
                }

                if (jobScheduleConfigs.Any())
                {
                    await ClearTasksAsync();
                }

                foreach (var config in jobScheduleConfigs)
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

                    var task = new JobScheduleTask(_logger, _scheduler);
                    await task.ScheduleAsync(config);
                    lock (_tasks)
                    {
                        _tasks.Add(task);
                    }
                    _logger.LogInformation($"Schedule Task:{JsonSerializer.Serialize(config)}");
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

                if (_hashCode == json.GetHashCode())
                {
                    return LoadConfigResult.NotChanged;
                }
                jobScheduleConfigs = JsonSerializer.Deserialize<JobScheduleConfig[]>(json);
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
