using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftpservertool
{
    public class UpdateConfigParameters
    {
        public string channel { get; set; }
        public string configRoot { get; set; }
        public bool initOnly { get; set; }

        public string[] hostNameList { get; set; }

        public string filterType { get; set; }

        public bool deleteonly {  get; set; }
    }
}
