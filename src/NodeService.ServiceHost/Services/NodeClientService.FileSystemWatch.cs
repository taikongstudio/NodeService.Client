using NodeService.Infrastructure.Concurrent;
using System.Collections.Concurrent;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {
        class FileSystemWatcherInfo
        {
            public FileSystemWatcher Watcher { get; set; }

            public FileSystemWatchConfigModel Configuration { get; set; }

        }

        ConcurrentDictionary<string, FileSystemWatcherInfo> _watchers;
        readonly IAsyncQueue<FileSystemWatchEventReport> _fileSystemWatchEventQueue;

        void InitializeFileSystemWatch()
        {
            _watchers = new ConcurrentDictionary<string, FileSystemWatcherInfo>();
        }

        void DeleteFileSystemWatcherInfo(FileSystemWatchConfigModel config)
        {
            if (!this._watchers.TryRemove(config.Id, out var info))
            {
                return;
            }
            _logger.LogInformation($"Delete {nameof(FileSystemWatcher)} {config.Id}");
            info.Watcher.EnableRaisingEvents = false;
            DetachFileSystemWatcherEvents(info.Watcher);
            info.Watcher.Dispose();
        }

        void AddOrUpdateFileSystemWatchConfiguration(FileSystemWatchConfigModel config)
        {
            this._watchers.AddOrUpdate(config.Id,
                (key) => AddFileSystemWatcherInfo(config),
                (key, oldValue) => UpdateFileSystemWatcherInfo(oldValue, config));

        }

        FileSystemWatcherInfo AddFileSystemWatcherInfo(FileSystemWatchConfigModel config)
        {
            _logger.LogInformation($"Add {nameof(FileSystemWatcher)} {config.Id}");
            FileSystemWatcherInfo fileSystemWatcherInfo = new FileSystemWatcherInfo();
            fileSystemWatcherInfo.Watcher = new FileSystemWatcher();
            fileSystemWatcherInfo.Configuration = config;
            AttachFileSystemWatcherEvents(fileSystemWatcherInfo.Watcher);
            UpdateFileSystemWatcherInfo(fileSystemWatcherInfo, config);
            return fileSystemWatcherInfo;
        }


        FileSystemWatcherInfo UpdateFileSystemWatcherInfo(
            FileSystemWatcherInfo info,
            FileSystemWatchConfigModel config)
        {
            _logger.LogInformation($"Update {nameof(FileSystemWatcher)} {config.Id}");
            var watcher = info.Watcher;
            watcher.EnableRaisingEvents = false;
            watcher.Path = Path.Combine(config.Path, config.RelativePath ?? string.Empty);
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


        bool TryFindConfigId(object sender, out string? configId)
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


        FileSystemWatchEventInfo CreateEventInfo(
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

                if (Directory.Exists(fullPath))
                {
                    var directoryInfo = new DirectoryInfo(fullPath);
                    var objectInfo = new VirtualFileSystemObjectInfo()
                    {
                        CreationTime = directoryInfo.CreationTime,
                        FullName = directoryInfo.FullName,
                        LastWriteTime = directoryInfo.LastWriteTime,
                        Length = 0,
                        Name = directoryInfo.Name,
                        Type = VirtualFileSystemObjectType.File,
                    };
                    eventInfo.Properties.Add(nameof(DirectoryInfo), JsonSerializer.Serialize(objectInfo));
                }
                else if (File.Exists(fullPath))
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
                    eventInfo.Properties.Add(nameof(FileInfo), JsonSerializer.Serialize(objectInfo));
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            return eventInfo;
        }

        FileSystemWatchRenameInfo CreateRenamedEventInfo(
            WatcherChangeTypes watcherChangeTypes,
            string fullPath,
            string name,
            string oldFullPath,
            string oldName
            )
        {
            var eventInfo = new FileSystemWatchRenameInfo()
            {
                ChangeTypes = (FileSystemWatchChangeTypes)watcherChangeTypes,
                FullPath = fullPath,
                Name = name,
                OldFullPath = oldFullPath,
                OldName = oldName,
            };
            try
            {

                if (Directory.Exists(fullPath))
                {
                    var directoryInfo = new DirectoryInfo(fullPath);
                    var objectInfo = new VirtualFileSystemObjectInfo()
                    {
                        CreationTime = directoryInfo.CreationTime,
                        FullName = directoryInfo.FullName,
                        LastWriteTime = directoryInfo.LastWriteTime,
                        Length = 0,
                        Name = directoryInfo.Name,
                        Type = VirtualFileSystemObjectType.File,
                    };
                    eventInfo.Properties.Add(nameof(DirectoryInfo), JsonSerializer.Serialize(objectInfo));
                }
                else if (File.Exists(fullPath))
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
                    eventInfo.Properties.Add(nameof(FileInfo), JsonSerializer.Serialize(objectInfo));
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            return eventInfo;
        }


        void AttachFileSystemWatcherEvents(FileSystemWatcher fileSystemWatcher)
        {
            fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            fileSystemWatcher.Created += FileSystemWatcher_Created;
            fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
            fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
            fileSystemWatcher.Error += FileSystemWatcher_Error;
        }

        void DetachFileSystemWatcherEvents(FileSystemWatcher fileSystemWatcher)
        {
            fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
            fileSystemWatcher.Created -= FileSystemWatcher_Created;
            fileSystemWatcher.Deleted -= FileSystemWatcher_Deleted;
            fileSystemWatcher.Renamed -= FileSystemWatcher_Renamed;
            fileSystemWatcher.Error -= FileSystemWatcher_Error;
        }

        void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
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
                    StackTrace = ex.StackTrace ?? string.Empty
                }
            });
        }

        void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (!TryFindConfigId(sender, out string? configId) || configId == null)
            {
                return;
            }
            var eventInfo = CreateRenamedEventInfo(e.ChangeType, e.FullPath, e.Name, e.OldFullPath, e.OldName);
            _fileSystemWatchEventQueue.TryWrite(new FileSystemWatchEventReport()
            {
                ConfigurationId = configId,
                Renamed = eventInfo
            });
        }

        void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (!TryFindConfigId(sender, out string? configId) || configId == null)
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

        void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
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

        void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
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
