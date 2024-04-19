using MaccorUploadTool.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MaccorUploadTool.Models
{
    public class DataFile
    {
        public const int PageSize = 1024;

        public List<DataFileHeader> DataFileHeader { get; set; } = [];

        public IEnumerable<TimeData> TimeDatas { get; set; }

    }
}
