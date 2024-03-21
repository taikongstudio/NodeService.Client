
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorUploadTool.Models
{
    public class FileSystemChangeRecord
    {
        public string FullPath { get; set; }

        public string Name { get; set; }

        public WatcherChangeTypes ChangeTypes { get; set; }


        public MaccorDataUploadStat? Stat { get; set; }



    }
}
