using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NodeService.DeviceHost.Devices
{
    public abstract class Device
    {

        public string DeviceId { get; set; }

        public string DeviceName { get; set; }
        public abstract ValueTask<bool> ConnectAsync();

        public abstract ValueTask<bool> UpdateOptionsAsync(JsonElement options);

        public abstract ValueTask<bool> FetchDataAsync(CancellationToken cancellationToken = default);
    }
}
