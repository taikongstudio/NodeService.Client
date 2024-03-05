using JobsWorker.Shared.DataModels;
using System.Linq.Expressions;

namespace JobsWorkerWebService.Data
{
    public  static partial class DbContextExtensions
    {
        public static async Task<NodeInfoModel> LoadAsync(this ApplicationDbContext dbContext, NodeInfoModel nodeInfoModel)
        {
            {
                var entry = dbContext.Entry(nodeInfoModel);

                await entry.Collection(x => x.PropertySnapshotGroups).LoadAsync();

                await entry.Collection(x => x.NodeInfoJobScheduleConfigBindingList).LoadAsync();

                await entry.Collection(x => x.JobExecutionInstances).LoadAsync();

                await entry.Navigation(nameof(NodeInfoModel.Profile)).LoadAsync();

                await entry.Navigation(nameof(NodeInfoModel.ActiveNodeConfigTemplate)).LoadAsync();

                await entry.Navigation(nameof(NodeInfoModel.LastNodePropertySnapshot)).LoadAsync();
            }


            return nodeInfoModel;
        }

        public static async Task<NodeInfoModel> AddOrUpdateAsync(this ApplicationDbContext dbContext, NodeInfoModel src)
        {
            ArgumentNullException.ThrowIfNull(dbContext, nameof(dbContext));
            ArgumentNullException.ThrowIfNull(src, nameof(src));

            var dest = await dbContext.NodeInfoDbSet.FindAsync(src.Id);

            if (dest == null)
            {
                var entry = await dbContext
                     .NodeInfoDbSet
                     .AddAsync(src);
                dest = entry.Entity;
            }
            else
            {
                await dbContext.LoadAsync(dest);
            }

            dest.Id = src.Id;
            dest.Name = src.Name;
            dest.Status = src.Status;
            dest.ActiveNodeConfigTemplateForeignKey = src.ActiveNodeConfigTemplateForeignKey;
            dest.LastNodePropertySnapshotForeignKey = src.LastNodePropertySnapshotForeignKey;
            dest.Profile = src.Profile;
            dest.ProfileForeignKey = src.ProfileForeignKey;
            dbContext.UpdateBindingCollection(dest.NodeInfoJobScheduleConfigBindingList, src.NodeInfoJobScheduleConfigBindingList);
            return dest;
        }

    }
}
