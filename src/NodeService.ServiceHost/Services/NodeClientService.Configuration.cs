namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {

        private Task ProcessConfigurationChangedReportAsync(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken)
        {

            var changedReport = subscribeEvent.ConfigurationChangedReport;
            foreach (var item in changedReport.Configurations)
            {
                try
                {
                    var id = item.Key;
                    ProcessConfigurationChangedEvent(item.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }
            return Task.CompletedTask;
        }

        private void ProcessConfigurationChangedEvent(string value)
        {
            var changedEvent = JsonSerializer.Deserialize<ConfigurationChangedEvent>(value);
            if (changedEvent == null)
            {
                return;
            }
            var type = typeof(JsonBasedDataModel).Assembly.GetType(changedEvent.TypeName);
            if (type == null)
            {
                return;
            }
            switch (type.Name)
            {
                case nameof(FileSystemWatchConfigModel):
                    ProcessFileSystemWatchConfigurationChangedEvent(changedEvent);
                    break;
                default:
                    break;
            }
        }

        private void ProcessFileSystemWatchConfigurationChangedEvent(ConfigurationChangedEvent changedEvent)
        {
            var fileSystemWatchConfig = JsonSerializer.Deserialize<FileSystemWatchConfigModel>(changedEvent.Json);
            if (fileSystemWatchConfig == null)
            {
                return;
            }
            switch (changedEvent.ChangedType)
            {
                case ConfigurationChangedType.None:
                    break;
                case ConfigurationChangedType.Add:
                case ConfigurationChangedType.Update:
                    this.AddOrUpdateFileSystemWatchConfiguration(fileSystemWatchConfig);
                    break;
                case ConfigurationChangedType.Delete:
                    this.DeleteFileSystemWatcherInfo(fileSystemWatchConfig);
                    break;
                default:
                    break;
            }
        }
    }
}
