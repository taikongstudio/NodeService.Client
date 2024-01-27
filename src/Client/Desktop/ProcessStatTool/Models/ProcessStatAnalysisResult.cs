using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessStatTool.Models
{

    public class Entry
    {
        public string Name { get; set; }

        public bool Exists { get; set; }
    }

    public class ProcessStatAnalysisResult
    {
        public List<Entry> Entries { get; set; }


    }
}
