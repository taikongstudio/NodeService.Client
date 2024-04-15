using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANDCexSharp
{
    public class BriefInfo
    {
        public int BoxNo { get; set; }

        public int Channel { get; set; }

        public int DownId { get; set; }

        public int DownVersion { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public bool IsTimeHighResolution { get; set; }

        public string FormationName { get; set; }
        public string BattNo { get; internal set; }

        public List<string> ChannelFiles = new List<string>();
    }
}
