namespace JobsWorkerWebService.Services.VirtualSystem
{
    public class VirtualFileSystemConfig
    {

        public string nodeConfigPathFormat { get; set; }

        public string fileCachesPathDir { get; set; }

        public string pluginPathFormat { get; set; }

        public string taskLogsPathFormat { get; set; }

        public string RequestUri { get; set; }

        public string GetConfigPath(string nodeName, string configName)
        {
            return this.nodeConfigPathFormat
                .Replace(nameof(nodeName), nodeName)
                .Replace(nameof(configName), configName);
        }

        public string GetPluginPath(string pluginId)
        {
            return this.pluginPathFormat
                .Replace($"{{{nameof(pluginId)}}}", pluginId);
        }

        public string GetFileCachePath(string nodeName)
        {
            return this.fileCachesPathDir
                .Replace($"{{{nameof(nodeName)}}}", nodeName);
        }

        public string GetTaskLogsPath(string nodeName, string instanceid)
        {
            return this.taskLogsPathFormat
                .Replace($"{{{nameof(nodeName)}}}", nodeName)
                .Replace($"{{{nameof(instanceid)}}}", instanceid);
        }
    }
}
