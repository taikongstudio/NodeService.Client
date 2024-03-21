using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.Daemon.Models
{

    public class FileSystemEventInfo
    {
        public string FullPath { get; set; }

        public WatcherChangeTypes ChangeTypes { get; set; }

    }
}
