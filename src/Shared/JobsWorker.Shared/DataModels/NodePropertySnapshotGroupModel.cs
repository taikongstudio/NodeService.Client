using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public class NodePropertySnapshotGroupModel : ModelBase
    {
        public string NodeInfoForeignKey { get; set; }
        [JsonIgnore]
        public NodeInfoModel NodeInfo { get; set; }

        public List<NodePropertySnapshotModel> Snapshots { get; set; } = [];



    }
}
