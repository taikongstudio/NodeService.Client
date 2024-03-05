using JobsWorker.Shared.DataModels;
using System.Linq.Expressions;

namespace JobsWorkerWebService.Data
{
    public  static partial class DbContextExtensions
    {

        public static async Task<NodeConfigTemplateModel> LoadAsync(this ApplicationDbContext dbContext, NodeConfigTemplateModel nodeConfigTemplate)
        {
            var entry = dbContext.Entry(nodeConfigTemplate);

            await entry.Collection(x => x.FtpConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.FtpConfigs).LoadAsync();

            await entry.Collection(x => x.FtpUploadConfigs).LoadAsync();

            await entry.Collection(x => x.FtpUploadConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.LogUploadConfigs).LoadAsync();

            await entry.Collection(x => x.LogUploadConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.MysqlConfigs).LoadAsync();

            await entry.Collection(x => x.MysqlConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.KafkaConfigs).LoadAsync();

            await entry.Collection(x => x.KafkaConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.PluginConfigs).LoadAsync();

            await entry.Collection(x => x.PluginConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.JobScheduleConfigs).LoadAsync();

            await entry.Collection(x => x.JobScheduleConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.LocalDirectoryMappingConfigs).LoadAsync();

            await entry.Collection(x => x.LocalDirectoryMappingConfigTemplateBindingList).LoadAsync();

            await entry.Collection(x => x.Nodes).LoadAsync();

            return nodeConfigTemplate;
        }

        public static async Task<NodeConfigTemplateModel> AddOrUpdateAsync(this ApplicationDbContext dbContext, NodeConfigTemplateModel src)
        {
            ArgumentNullException.ThrowIfNull(dbContext, nameof(dbContext));
            ArgumentNullException.ThrowIfNull(src, nameof(src));

            var dest = await dbContext.NodeConfigTemplateDbSet.FindAsync(src.Id);

            if (dest == null)
            {
                var entry = await dbContext
                     .NodeConfigTemplateDbSet
                     .AddAsync(src);
                dest = entry.Entity;
            }
            else
            {
                await dbContext.LoadAsync(dest);
            }

            dest.Id = src.Id;
            dest.Name = src.Name;
            dest.Description = src.Description;
            dest.GrpcAddress = src.GrpcAddress;
            dest.Version = src.Version;
            dest.HttpAddress = src.HttpAddress;
            dest.ModifiedDateTime = src.ModifiedDateTime;
            dest.IsDefault = src.IsDefault;
            dbContext.UpdateBindingCollection(dest.FtpConfigTemplateBindingList, src.FtpConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.FtpUploadConfigTemplateBindingList, src.FtpUploadConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.LocalDirectoryMappingConfigTemplateBindingList, src.LocalDirectoryMappingConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.LogUploadConfigTemplateBindingList, src.LogUploadConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.MysqlConfigTemplateBindingList, src.MysqlConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.PluginConfigTemplateBindingList, src.PluginConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.RestApiConfigTemplateBindingList, src.RestApiConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.JobScheduleConfigTemplateBindingList, src.JobScheduleConfigTemplateBindingList);
            dbContext.UpdateBindingCollection(dest.KafkaConfigTemplateBindingList, src.KafkaConfigTemplateBindingList);
            await dbContext.UpdateCollectionAsync(dest.Nodes,
                src.NodeIdList.Select((x) => dbContext.NodeInfoDbSet.Find(x)),
                AddOrUpdateNodeInfoAsync,
                RemoveNodeInfoFromBindingListAsync
                );
            return dest;

            async Task AddOrUpdateNodeInfoAsync(NodeInfoModel nodeInfo)
            {
                await dbContext.LoadAsync(nodeInfo);
                dbContext.UpdateBindingCollection(nodeInfo.NodeInfoJobScheduleConfigBindingList, src.JobScheduleConfigTemplateBindingList.Select(x => new NodeInfoJobScheduleConfigBindingModel()
                {
                    OwnerForeignKey = nodeInfo.Id,
                    TargetForeignKey = x.TargetForeignKey,
                }));

            }

            async Task RemoveNodeInfoFromBindingListAsync(NodeInfoModel nodeInfo)
            {
                await dbContext.LoadAsync(nodeInfo);
                nodeInfo.ActiveNodeConfigTemplateForeignKey = null;
            }
        }




    }
}
