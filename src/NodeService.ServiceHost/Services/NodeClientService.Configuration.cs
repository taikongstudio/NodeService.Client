using Confluent.Kafka;
using NodeService.Infrastructure.Models;
using System.Configuration;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {

        private async Task ProcessConfigurationChangedReportAsync(
            NodeServiceClient client,
            SubscribeEvent subscribeEvent,
            CancellationToken cancellationToken)
        {
            foreach (var kv in subscribeEvent.ConfigurationChangedReport.Configurations)
            {
                var eventReport = new FileSystemWatchEventReport()
                {
                    RequestId = subscribeEvent.RequestId,
                };
                try
                {
                    var key = kv.Key;
                    var segments = key.Split("_", StringSplitOptions.RemoveEmptyEntries);
                    eventReport.ConfigurationId = segments[1];
                    ProcessConfigurationChangedEvent(kv.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    eventReport.Error = new ExceptionInfo();
                    eventReport.Error.ErrorCode = ex.HResult;
                    eventReport.Error.Message = ex.Message;
                    eventReport.Error.StackTrace = ex.StackTrace;
                }
                await _fileSystemWatchEventQueue.EnqueueAsync(eventReport);
            }
        }

        private void ProcessConfigurationChangedEvent(string json)
        {
            var changedEvent = JsonSerializer.Deserialize<ConfigurationChangedEvent>(json);
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
