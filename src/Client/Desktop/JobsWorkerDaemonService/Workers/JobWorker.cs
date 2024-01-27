using JobsWorkerDaemonService.Models;
using Quartz;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace JobsWorkerDaemonService.Workers
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
            this._logger.LogInformation(message);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                this._logger.LogInformation($"Start {nameof(JobWorker)}");
                this._localServerConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "server.bat");
                if (!FtpServerConfig.TryLoadServerConfig(this._localServerConfigPath, this._logger, out FtpServerConfig ftpServerConfig))
                {
                    MyLog("Load server config fail");
                    return;
                }

                if (ftpServerConfig == null)
                {
                    MyLog("Load server config fail");
                    return;
                }
                this._watchPath = Path.Combine(AppContext.BaseDirectory, "config");
                this._configPath = Path.Combine(AppContext.BaseDirectory, "config", ftpServerConfig.version, "config.bat");

                this._scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
                await this._scheduler.Start(stoppingToken);
                if (!Directory.Exists(this._watchPath))
                {
                    Directory.CreateDirectory(this._watchPath);
                }
                this._fileSystemWatcher = new FileSystemWatcher(this._watchPath);
                this._fileSystemWatcher.IncludeSubdirectories = true;
                this._fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
    | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.LastAccess
    | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.Security;
                this._fileSystemWatcher.Created += _fileSystemWatcher_Created;
                this._fileSystemWatcher.Error += _fileSystemWatcher_Error;
                this._fileSystemWatcher.Deleted += _fileSystemWatcher_Deleted;
                this._fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
                this._fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
                this._fileSystemWatcher.EnableRaisingEvents = true;

                await TryLoadTasksFromConfigAsync();
            }
            catch (Exception ex)
            {
                this._logger.LogInformation(ex.ToString());
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
                    this._logger.LogWarning($"Could not found config:{this._configPath}");
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
                        this._logger.LogInformation("Skip schedule reason:disabled");
                        continue;
                    }
                    if (!FilterHostName(config))
                    {
                        this._logger.LogInformation("Skip schedule reason:host filter");
                        continue;
                    }

                    var task = new JobScheduleTask(_logger, _scheduler);
                    await task.ScheduleAsync(config);
                    lock (this._tasks)
                    {
                        this._tasks.Add(task);
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
                this._logger.LogError(ex.ToString());
            }
            return false;
        }

        private async Task ClearTasksAsync()
        {
            JobScheduleTask[] tasks = Array.Empty<JobScheduleTask>();
            lock (this._tasks)
            {
                tasks = this._tasks.ToArray();
                this._tasks.Clear();
            }

            foreach (var task in tasks)
            {
                await task.CancelAsync();
            }
            this._logger.LogInformation($"Clear Tasks:{tasks.Length}");
        }

        private LoadConfigResult TryLoadJsonConfig(out JobScheduleConfig[] jobScheduleConfigs)
        {
            jobScheduleConfigs = Array.Empty<JobScheduleConfig>();
            try
            {
                if (!File.Exists(this._configPath))
                {
                    return LoadConfigResult.LoadFail;
                }
                string json = null;
                PathLocker.Lock(this._configPath, () =>
                {
                    json = File.ReadAllText(this._configPath);
                });

                if (this._hashCode == json.GetHashCode())
                {
                    return LoadConfigResult.NotChanged;
                }
                jobScheduleConfigs = JsonSerializer.Deserialize<JobScheduleConfig[]>(json);
                this._logger.LogInformation("Load config success:");
                this._hashCode = json.GetHashCode();
                return LoadConfigResult.Changed;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
                return LoadConfigResult.LoadFail;
            }

        }

    }
}
