using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{

    public class NodeInfoModel : ModelBase
    {

        [Required]
        public string ProfileForeignKey { get; set; }
        [Required]
        public NodeProfileModel Profile { get; set; }

        [Required]
        public List<NodePropertySnapshotGroupModel> PropertySnapshotGroups { get; set; } = [];

        [Required]
        public List<NodeInfoJobScheduleConfigBindingModel> NodeInfoJobScheduleConfigBindingList { get; set; } = [];

        [Required]
        public List<JobExecutionInstanceModel> JobExecutionInstances { get; set; } = [];

        [Required]
        public List<JobScheduleConfigModel> JobScheduleConfigs { get; set; } = [];

        public string? ActiveNodeConfigTemplateForeignKey {  get; set; }
        [JsonIgnore]
        public NodeConfigTemplateModel? ActiveNodeConfigTemplate { get; set; }

        public string? LastNodePropertySnapshotForeignKey { get; set; }

        [ForeignKey(nameof(LastNodePropertySnapshotForeignKey))]
        [JsonIgnore]
        public NodePropertySnapshotModel? LastNodePropertySnapshot { get; set; }

        public NodeStatus Status { get; set; }

        public static NodeInfoModel Create(string nodeName)
        {
            var nodeInfoModel = new NodeInfoModel()
            {
                Id = Guid.NewGuid().ToString(),
                Name = nodeName,
            };

            nodeInfoModel.Profile = new NodeProfileModel()
            {
                Id = Guid.NewGuid().ToString(),
                Name = nodeName
            };
            nodeInfoModel.PropertySnapshotGroups.Add(new NodePropertySnapshotGroupModel()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Default",
                NodeInfoForeignKey = nodeInfoModel.Id,
            });

            nodeInfoModel.ProfileForeignKey = nodeInfoModel.Profile.Id;
            return nodeInfoModel;
        }

    }
}
