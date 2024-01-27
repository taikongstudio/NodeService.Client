﻿
using FluentFTP;
using JobsWorkerDaemonService.Jobs;
using JobsWorkerDaemonService.Models;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace JobsWorkerDaemonService.Workers
{
    internal class UploadLogsJobWorker : BackgroundService
    {
        private readonly ILogger<UploadLogsJobWorker> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private IScheduler _scheduler;
        private string _ftpServerConfigPath;
        private string _watchPath;
        private FileSystemWatcher _fileSystemWatcher;
        private List<JobScheduleTask> _processMonitorTasks;
        private int _hashCode;
        private Channel<FileSystemEventInfo> _channel;

        public UploadLogsJobWorker(ILogger<UploadLogsJobWorker> logger, ISchedulerFactory schedulerFactory)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _processMonitorTasks = new List<JobScheduleTask>();
            _channel = Channel.CreateUnbounded<FileSystemEventInfo>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                this._logger.LogInformation($"Start {nameof(UploadLogsJobWorker)}");
                this._watchPath = Path.Combine(AppContext.BaseDirectory, "config");
                this._ftpServerConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "server.bat");
                if (!Directory.Exists(this._watchPath))
                {
                    Directory.CreateDirectory(this._watchPath);
                }
                this._scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
                await _scheduler.Start(stoppingToken);
                this._fileSystemWatcher = new FileSystemWatcher(_watchPath);
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
                                    this._logger.LogInformation("Created");
                                    await TryLoadTasksFromConfigAsync();
                                }
                                catch (Exception ex)
                                {
                                    this._logger.LogError(ex.ToString());
                                }
                                break;
                            case WatcherChangeTypes.Deleted:
                                try
                                {
                                    this._logger.LogInformation("Deleted");
                                    await ClearTasksAsync();
                                }
                                catch (Exception ex)
                                {
                                    this._logger.LogError(ex.ToString());
                                }
                                break;
                            case WatcherChangeTypes.Changed:
                                try
                                {
                                    this._logger.LogInformation("Changed");
                                    await TryLoadTasksFromConfigAsync();
                                }
                                catch (Exception ex)
                                {
                                    this._logger.LogError(ex.ToString());
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
                    this._logger.LogInformation(ex.ToString());
                }
                this._logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            Environment.Exit(0);
        }

        private async void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            await this._channel.Writer.WriteAsync(new FileSystemEventInfo()
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
            await this._channel.Writer.WriteAsync(new FileSystemEventInfo()
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
            await this._channel.Writer.WriteAsync(new FileSystemEventInfo()
            {
                ChangeTypes = WatcherChangeTypes.Created,
                FullPath = e.FullPath,
            });

        }

        private void MyLog(string message)
        {
            this._logger.LogInformation(message);
        }

        private async Task TryLoadTasksFromConfigAsync()
        {
            try
            {
                var loadConfigResult = FtpServerConfig.TryLoadServerConfig(
                    this._ftpServerConfigPath, 
                    ref this._hashCode, 
                    this._logger, 
                    out var ftpConfig);

                if (loadConfigResult == LoadConfigResult.LoadFail)
                {
                    MyLog("Load Fail");
                    return;
                }

                if (loadConfigResult == LoadConfigResult.NotChanged)
                {
                    MyLog("Not Changed");
                    return;
                }

                if (ftpConfig != null)
                {
                    await this.ClearTasksAsync();
                }

                var processMonitorTask = new JobScheduleTask(_logger, _scheduler);
                await processMonitorTask.ScheduleAsync(ftpConfig.AsJobScheduleConfig(typeof(UploadlogsToFtpServerJob).FullName));
                lock (this._processMonitorTasks)
                {
                    this._processMonitorTasks.Add(processMonitorTask);
                }
                this._logger.LogInformation($"Schedule Task:{JsonSerializer.Serialize(ftpConfig)}");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
            }
            finally
            {

            }

        }

        private async Task ClearTasksAsync()
        {
            JobScheduleTask[] tasks = Array.Empty<JobScheduleTask>();
            lock (this._processMonitorTasks)
            {
                tasks = this._processMonitorTasks.ToArray();
                this._processMonitorTasks.Clear();
            }
            foreach (var task in tasks)
            {
                await task.CancelAsync();
            }
            _logger.LogInformation($"Clear Tasks:{tasks.Length}");
        }



    }
}
