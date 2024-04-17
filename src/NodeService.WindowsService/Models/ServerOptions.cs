using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Models
{
    public class ServerOptions
    {
        public string GrpcAddress {  get; set; }

        public string HttpAddress { get; set;}

    }
}
