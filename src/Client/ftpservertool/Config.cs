using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftpservertool
{
    public class config
    {
        public string host { get; set; }

        public int port { get; set; }

        public string username { get; set; }

        public string password { get; set; }

        public string action { get; set; }

        public string[] args { get; set; }


    }
}
