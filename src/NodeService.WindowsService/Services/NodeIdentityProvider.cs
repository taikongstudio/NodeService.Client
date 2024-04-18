namespace NodeService.WindowsService.Services
{
    public class NodeIdentityProvider : INodeIdentityProvider
    {
        public string GetIdentity()
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
            string nodeIdentity = null;
            if (!nodeRegisty.GetValueNames().Any(x => x == nameof(NodeClientHeaders.NodeId)))
            {
                nodeIdentity = Guid.NewGuid().ToString();
                nodeRegisty.SetValue(nameof(NodeClientHeaders.NodeId), nodeIdentity);

            }
            nodeIdentity = nodeRegisty.GetValue(nameof(NodeClientHeaders.NodeId)) as string;
            nodeRegisty.Dispose();
            return nodeIdentity;
        }

    }
}
