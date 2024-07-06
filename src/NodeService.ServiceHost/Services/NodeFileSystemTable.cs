using NodeService.Infrastructure.NodeFileSystem;
using System.Collections.Concurrent;

namespace NodeService.ServiceHost.Services
{
    public class NodeFileSystemTable
    {
        private ConcurrentDictionary<int, NodeFileSystemWatchRecord> _inMemoryRecords;

        private ConcurrentDictionary<int, string> _stringTable;

        public NodeFileSystemTable()
        {
            _inMemoryRecords = new ConcurrentDictionary<int, NodeFileSystemWatchRecord>();
            _stringTable = new ConcurrentDictionary<int, string>();
        }

        public bool ContainsKey(string fullPath)
        {
            var hashCode = fullPath.GetHashCode();
            return _inMemoryRecords.ContainsKey(hashCode);
        }

        public void AddOrUpdate(string fullPath, NodeFileSystemWatchRecord nodeFileSystemWatchRecord)
        {
            var key = fullPath.GetHashCode();
            this._inMemoryRecords.AddOrUpdate(key, nodeFileSystemWatchRecord, (key, oldValue) => nodeFileSystemWatchRecord);
        }

        public bool TryGetValue(string fullPath, out NodeFileSystemWatchRecord? nodeFileSystemWatchRecord)
        {
            var key = fullPath.GetHashCode();
            return _inMemoryRecords.TryGetValue(key, out nodeFileSystemWatchRecord);
        }

        public void TryRemove(string fullPath)
        {
            var key = fullPath.GetHashCode();
            this._inMemoryRecords.TryRemove(key, out _);
        }

    }
}
