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
        public long Speed { get; set; }
        public bool IsReceiveOnly { get; set; }
        public bool SupportsMulticast { get; set; }

        public IPv4InterfaceStatisticsModel? IPv4InterfaceStatistics { get; set; }

        public static NetworkInterfaceModel From(NetworkInterface networkInterface)
        {
            NetworkInterfaceModel networkInterfaceModel = new NetworkInterfaceModel();
            try
            {
                networkInterfaceModel.Name = networkInterface.Name;
                networkInterfaceModel.PhysicalAddress = BitConverter.ToString(networkInterface.GetPhysicalAddress().GetAddressBytes()).Replace('-', ':');
                networkInterfaceModel.NetworkInterfaceType = networkInterface.NetworkInterfaceType;
                networkInterfaceModel.Id = networkInterface.Id;
                networkInterfaceModel.OperationalStatus = networkInterface.OperationalStatus;
                networkInterfaceModel.Description = networkInterface.Description;
                networkInterfaceModel.Speed = networkInterface.Speed;
                networkInterfaceModel.IsReceiveOnly = networkInterface.IsReceiveOnly;
                networkInterfaceModel.SupportsMulticast = networkInterface.SupportsMulticast;
                networkInterfaceModel.IPv4InterfaceStatistics = IPv4InterfaceStatisticsModel.From(networkInterface.GetIPv4Statistics());
            }
            catch (Exception ex)
            {

            }

            return networkInterfaceModel;
        }

    }
}
