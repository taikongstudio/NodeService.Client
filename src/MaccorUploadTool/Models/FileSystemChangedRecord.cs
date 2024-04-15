
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorUploadTool.Models
{
    public class FileSystemChangedRecord
    {
        public string LocalFilePath { get; set; }

        public string OriginalFilePath { get; set; }

        public string Name { get; set; }

        public WatcherChangeTypes ChangeTypes { get; set; }

        public DataFile DataFile { get; set; }

        public MaccorDataUploadStat? Stat { get; set; }

        public FileRecordModel FileRecord { get; set; }

        public string FileHashValue {  get; set; }


        public int Index { get; set; }


    }
}
