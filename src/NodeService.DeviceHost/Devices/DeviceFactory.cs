using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NodeService.DeviceHost.Devices
{
    public class DeviceFactory
    {
        readonly IServiceProvider _serviceProvider;
        readonly JsonSerializerOptions _options;

        public DeviceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async ValueTask<bool> TryUpdateDeviceOptionsAsync(ApiService apiService, Device device, NodeInfoModel nodeInfo, CancellationToken cancellationToken)
        {
            if (!nodeInfo.Properties.TryGetValue("Settings", out var value))
            {
                return false;
            }
            if (value is not JsonElement jsonElement)
            {
                return false;
            }
            switch (nodeInfo.Profile.Manufacturer)
            {
                case "CHONG_QING_YIN_HE":
                    await device.UpdateOptionsAsync(jsonElement);
                    break;
                default:
                    break;
            }
            return true;
        }

        public ValueTask<Device?> TryCreateDeviceAsync(ApiService apiService, NodeInfoModel nodeInfo, CancellationToken cancellationToken)
        {
            Device? device = null;
            if (!nodeInfo.Properties.TryGetValue("Settings", out var value))
            {
                return ValueTask.FromResult<Device?>(null);
            }
            if (value is not JsonElement jsonElement)
            {
                return ValueTask.FromResult<Device?>(null);
            }
            switch (nodeInfo.Profile.Manufacturer)
            {
                case "CHONG_QING_YIN_HE":
                    var hostPortSettings = jsonElement.Deserialize<HostPortSettings>(_options);
                    device = ActivatorUtilities.CreateInstance<ChongQingYinHeDevice>(_serviceProvider, new YinHeDeviceOptions()
                    {
                        Host = hostPortSettings.IpAddress,
                        Port = hostPortSettings.Port,
                        SamplingDurationInSeconds = Math.Max(hostPortSettings.SamplingDurationInSeconds, 30)
                    });
                    break;
                default:
                    break;
            }
            if (device != null)
            {
                device.DeviceId = nodeInfo.Id;
                device.DeviceName = nodeInfo.Name;
            }
            return ValueTask.FromResult<Device?>(device);
        }

    }
}
