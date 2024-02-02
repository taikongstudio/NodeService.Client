using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class IPv4InterfaceStatisticsModel
    {
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }
        public long IncomingPacketsDiscarded { get; set; }
        public long IncomingPacketsWithErrors { get; set; }
        public long IncomingUnknownProtocolPackets { get; set; }
        public long NonUnicastPacketsReceived { get; set; }
        public long NonUnicastPacketsSent { get; set; }

        public long OutgoingPacketsDiscarded { get; set; }
        public long OutgoingPacketsWithErrors { get; set; }
        public long OutputQueueLength { get; set; }
        public long UnicastPacketsReceived { get; set; }
        public long UnicastPacketsSent { get; set; }

        public static IPv4InterfaceStatisticsModel From(IPv4InterfaceStatistics statistics)
        {
            IPv4InterfaceStatisticsModel statisticsModel = new IPv4InterfaceStatisticsModel();
            try
            {
                statisticsModel.BytesReceived = statistics.BytesReceived;
                statisticsModel.BytesSent = statistics.BytesSent;
                statisticsModel.IncomingPacketsDiscarded = statistics.IncomingPacketsDiscarded;
                statisticsModel.IncomingPacketsWithErrors = statistics.IncomingPacketsWithErrors;
                statisticsModel.IncomingUnknownProtocolPackets = statistics.IncomingUnknownProtocolPackets;
                statisticsModel.NonUnicastPacketsSent = statistics.NonUnicastPacketsSent;
                statisticsModel.NonUnicastPacketsReceived = statistics.NonUnicastPacketsReceived;
                statisticsModel.OutgoingPacketsWithErrors = statistics.OutgoingPacketsWithErrors;
                statisticsModel.OutputQueueLength = statistics.OutputQueueLength;
                statisticsModel.UnicastPacketsSent = statistics.UnicastPacketsSent;
                statisticsModel.UnicastPacketsReceived = statistics.UnicastPacketsReceived;

            }
            catch (Exception ex)
            {

            }
            return statisticsModel;
        }

    }
}
