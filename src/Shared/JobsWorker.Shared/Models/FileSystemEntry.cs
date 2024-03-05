using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class FileSystemEntry
    {
        public DateTime CreationTime { get; set; }

        public string FullName { get; set; }

        public DateTime LastWriteTime { get; set; }

        public long Length { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }
    }
}
