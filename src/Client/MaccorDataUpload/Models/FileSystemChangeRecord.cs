using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorDataUpload.Models
{
    public class FileSystemChangeRecord
    {
        public string FullPath { get; set; }

        public string Name { get; set; }

        public WatcherChangeTypes ChangeTypes { get; set; }


        public UploadFileStat? UploadFileStat { get; set; }



    }
}
