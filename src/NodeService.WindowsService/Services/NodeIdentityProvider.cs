using Microsoft.Win32;
using NodeService.Infrastructure.NodeSessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class NodeIdentityProvider : INodeIdentityProvider
    {
        public string GetNodeId()
        {
            const string ServiceName = "NodeService.WindowsService";
            using var softwareSubKey = Registry.LocalMachine.OpenSubKey("SOFTWARE", true);
            var subKeyNames = softwareSubKey.GetSubKeyNames();
            RegistryKey nodeRegisty = null;
            if (!subKeyNames.Any(x => x == ServiceName))
            {
                nodeRegisty = softwareSubKey.CreateSubKey(ServiceName, true);
            }
            else
            {
                nodeRegisty = softwareSubKey.OpenSubKey(ServiceName, true);
            }
            string machineId = null;
            if (!nodeRegisty.GetValueNames().Any(x => x == nameof(NodeClientHeaders.NodeId)))
            {
                machineId = Guid.NewGuid().ToString();
                nodeRegisty.SetValue(nameof(NodeClientHeaders.NodeId), machineId);

            }
            machineId = nodeRegisty.GetValue(nameof(NodeClientHeaders.NodeId)) as string;
            nodeRegisty.Dispose();
            return machineId;
        }

    }
}
