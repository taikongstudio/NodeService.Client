using NodeService.Infrastructure.Concurrent;
using System.Collections.Concurrent;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {
        private class FileSystemWatcherInfo
        {
            public FileSystemWatcher Watcher { get; set; }

            public FileSystemWatchConfigModel Configuration { get; set; }

        }
        private ConcurrentDictionary<string, FileSystemWatcherInfo> _watchers;
        private readonly IAsyncQueue<FileSystemWatchEventReport> _fileSystemWatchEventQueue;

        private void InitializeFileSystemWatch()
        {
            _watchers = new ConcurrentDictionary<string, FileSystemWatcherInfo>();
        }

        private void DeleteFileSystemWatcherInfo(FileSystemWatchConfigModel config)
        {
            if (!this._watchers.TryRemove(config.Id, out var info))
            {
                return;
            }
            info.Watcher.EnableRaisingEvents = false;
            DetachFileSystemWatcherEvents(info.Watcher);
            info.Watcher.Dispose();
        }

        private void AddOrUpdateFileSystemWatchConfiguration(FileSystemWatchConfigModel config)
        {
            this._watchers.AddOrUpdate(config.Id,
                (key) => AddFileSystemWatcherInfo(config),
                (key, oldValue) => UpdateFileSystemWatcherInfo(oldValue, config));

        }

        private FileSystemWatcherInfo AddFileSystemWatcherInfo(FileSystemWatchConfigModel config)
        {
            FileSystemWatcherInfo fileSystemWatcherInfo = new FileSystemWatcherInfo();
            fileSystemWatcherInfo.Watcher = new FileSystemWatcher();
            fileSystemWatcherInfo.Configuration = config;
            AttachFileSystemWatcherEvents(fileSystemWatcherInfo.Watcher);
            UpdateFileSystemWatcherInfo(fileSystemWatcherInfo, config);
            return fileSystemWatcherInfo;
        }


        private FileSystemWatcherInfo UpdateFileSystemWatcherInfo(
            FileSystemWatcherInfo info,
            FileSystemWatchConfigModel config)
        {
            var watcher = info.Watcher;
            watcher.EnableRaisingEvents = false;
            watcher.Path = config.Path;
            watcher.IncludeSubdirectories = config.IncludeSubdirectories;
            watcher.NotifyFilter = config.NotifyFilter;
            if (config.UseDefaultFilter)
            {
                watcher.Filter = config.Filter;

            }
            else
            {
                watcher.Filters.Clear();
                foreach (var filter in config.Filters)
                {
                    if (string.IsNullOrEmpty(filter.Value))
                    {
                        continue;
                    }
                    watcher.Filters.Add(filter.Value);
                }
            }
            watcher.EnableRaisingEvents = config.EnableRaisingEvents;
            info.Configuration = config;
            return info;
        }


        private bool TryFindConfigId(object sender, out string? configId)
        {
            configId = null;
            foreach (var item in this._watchers.Values)
            {
                if (item.Watcher == sender)
                {
                    configId = item.Configuration.Id;
                    break;
                }
            }
            return configId != null;
        }


        private FileSystemWatchEventInfo CreateEventInfo(
            WatcherChangeTypes watcherChangeTypes,
            string fullPath,
            string name)
        {
            var eventInfo = new FileSystemWatchEventInfo()
            {
                ChangeTypes = (FileSystemWatchChangeTypes)watcherChangeTypes,
                FullPath = fullPath,
                Name = name,
            };
            try
            {
                var fileInfo = new FileInfo(fullPath);
                var objectInfo = new VirtualFileSystemObjectInfo()
                {
                    CreationTime = fileInfo.CreationTime,
                    FullName = fileInfo.FullName,
                    LastWriteTime = fileInfo.LastWriteTime,
                    Length = fileInfo.Length,
                    Name = fileInfo.Name,
                    Type = VirtualFileSystemObjectType.File,
                };
                eventInfo.Properties.Add("FileInfo", JsonSerializer.Serialize(objectInfo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            return eventInfo;
        }


        private void AttachFileSystemWatcherEvents(FileSystemWatcher fileSystemWatcher)
        {
            fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            fileSystemWatcher.Created += FileSystemWatcher_Created;
            fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
            fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
            fileSystemWatcher.Error += FileSystemWatcher_Error;
        }

        private void DetachFileSystemWatcherEvents(FileSystemWatcher fileSystemWatcher)
        {
            fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
            fileSystemWatcher.Created -= FileSystemWatcher_Created;
            fileSystemWatcher.Deleted -= FileSystemWatcher_Deleted;
            fileSystemWatcher.Renamed -= FileSystemWatcher_Renamed;
            fileSystemWatcher.Error -= FileSystemWatcher_Error;
        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            if (!TryFindConfigId(sender, out string? configId) || configId == null)
            {
                return;
            }
            var ex = e.GetException();
            _fileSystemWatchEventQueue.TryWrite(new FileSystemWatchEventReport()
            {
                ConfigurationId = configId,
                Error = new ExceptionInfo()
                {
                    Message = ex.Message,
                    StackTrace = ex.StackTrace
                }
            });
        }

        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (!TryFindConfigId(sender, out string? configId) || configId == null)
            {
                return;
            }
            if (Directory.Exists(e.FullPath))
            {
                return;
            }
            _fileSystemWatchEventQueue.TryWrite(new FileSystemWatchEventReport()
            {
                ConfigurationId = configId,
                Renamed = new FileSystemWatchRenameInfo()
                {
                    ChangeTypes = (FileSystemWatchChangeTypes)e.ChangeType,
                    FullPath = e.FullPath,
                    Name = e.Name,
                    OldFullPath = e.OldFullPath,
                    OldName = e.OldName,
                }
            });
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (!TryFindConfigId(sender, out string? configId) || configId == null)
            {
                return;
            }
            if (Directory.Exists(e.FullPath))
            {
                return;
            }
            var eventInfo = CreateEventInfo(e.ChangeType, e.FullPath, e.Name);
            _fileSystemWatchEventQueue.TryWrite(new FileSystemWatchEventReport()
            {
                ConfigurationId = configId,
                Deleted = eventInfo
            });
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!TryFindConfigId(sender, out string? configId) || configId == null)
            {
                return;
            }
            var eventInfo = CreateEventInfo(e.ChangeType, e.FullPath, e.Name);
            _fileSystemWatchEventQueue.TryWrite(new FileSystemWatchEventReport()
            {
                ConfigurationId = configId,
                Created = eventInfo
            });
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!TryFindConfigId(sender, out string? configId) || configId == null)
            {
                return;
            }
            var eventInfo = CreateEventInfo(e.ChangeType, e.FullPath, e.Name);
            _fileSystemWatchEventQueue.TryWrite(new FileSystemWatchEventReport()
            {
                ConfigurationId = configId,
                Changed = eventInfo
            });
        }


    }
}
