using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessStatTool.Models
{
    public class ProcessStatInfo
    {
        public string ProcessName { get; set; }

        public string FileName { get; set; }
        public int Id { get; set; }
        public bool Responding { get; set; }
        public long VirtualMemorySize64 { get; set; }
        public string StartTime { get; set; }
        public string? ExitTime { get; set; }
        public long PagedMemorySize64 { get; set; }
        public long NonpagedSystemMemorySize64 { get; set; }
        public long PagedSystemMemorySize64 { get; set; }
        public int HandleCount { get; set; }
    }
}
