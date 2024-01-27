using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNode.Models
{
    public class PathLocker
    {
        private static ConcurrentDictionary<string, object> _dict = new ConcurrentDictionary<string, object>();

        public static void Lock(string path, Action action)
        {
            if (!_dict.TryGetValue(path, out var lockObject))
            {
                lockObject = new object();
                _dict.TryAdd(path, lockObject);
            }
            lock (lockObject)
            {
                action();
            }
            _dict.TryRemove(path, out _);
        }



    }
}
