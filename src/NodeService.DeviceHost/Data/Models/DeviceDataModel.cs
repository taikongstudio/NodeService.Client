using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.DeviceHost.Data.Models
{
    public class DeviceDataModel
    {
        public int Id { get; set; }

        public DateTime DateTime { get; set; }

        public string Host { get; set; }

        public string DeviceName { get; set; }

        public string DeviceManufacture { get; set; }

        public double Temperature { get; set; }

        public double Humidity { get; set; }

    }
}
