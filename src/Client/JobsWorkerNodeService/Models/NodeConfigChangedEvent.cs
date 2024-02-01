using JobsWorker.Shared.MessageQueue.Models;
using JobsWorker.Shared.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobsWorkerNodeService.Models
{
    public class NodeConfigChangedEvent : Message<NodeConfig>
    {
        [JsonIgnore]
        public CancellationToken CancellationToken { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
