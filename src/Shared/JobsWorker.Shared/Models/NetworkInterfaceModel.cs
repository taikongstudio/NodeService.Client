using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class NetworkInterfaceModel
    {
        public string Name { get; set; }

        public string PhysicalAddress { get; set; }

        public NetworkInterfaceType NetworkInterfaceType { get; set; }
        public string Id { get; set; }
        public OperationalStatus OperationalStatus { get; set; }
        public string Description { get; set; }
    }
}
