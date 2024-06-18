using Confluent.Kafka;
using NodeService.Infrastructure.Concurrent;
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
                    var changedEvent = JsonSerializer.Deserialize<ConfigurationChangedEvent>(kv.Value);
                    if (changedEvent == null)
                    {
                        continue;
                    }
                    await ProcessConfigurationChangedEvent(changedEvent);
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

        private async Task ProcessConfigurationChangedEvent(ConfigurationChangedEvent changedEvent)
        {

            var type = typeof(JsonBasedDataModel).Assembly.GetType(changedEvent.TypeName);
            if (type == null)
            {
                return;
            }
            switch (type.Name)
            {
                case nameof(FileSystemWatchConfigModel):
                    {
                        var fileSystemWatchConfig = JsonSerializer.Deserialize<FileSystemWatchConfigModel>(changedEvent.Json);
                        if (fileSystemWatchConfig == null)
                        {
                            return;
                        }
                        var opKind = changedEvent.ChangedType switch
                        {
                            ConfigurationChangedType.Add => BatchQueueOperationKind.InsertOrUpdate,
                            ConfigurationChangedType.Update => BatchQueueOperationKind.InsertOrUpdate,
                            ConfigurationChangedType.Delete => BatchQueueOperationKind.Delete,
                            ConfigurationChangedType.None or _ => BatchQueueOperationKind.None
                        };
                        var op = new BatchQueueOperation<FileSystemWatchConfigModel, bool>(fileSystemWatchConfig, opKind);
                        await _fileSystemConfigurationQueue.EnqueueAsync(op);
                        var result = await op.WaitAsync();
                    }
                    break;
                default:
                    break;
            }
        }


    }
}
