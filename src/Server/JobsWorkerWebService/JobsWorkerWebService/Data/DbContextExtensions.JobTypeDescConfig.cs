using JobsWorker.Shared.DataModels;
using System.Linq.Expressions;

namespace JobsWorkerWebService.Data
{
    public  static partial class DbContextExtensions
    {
        public static async Task<JobTypeDescConfigModel> LoadAsync(this ApplicationDbContext dbContext, JobTypeDescConfigModel model)
        {
            var entry = dbContext.Entry(model);

            await entry.Collection(x => x.JobScheduleConfigs).LoadAsync();

            return model;
        }

        public static async Task<JobTypeDescConfigModel> AddOrUpdateAsync(this ApplicationDbContext dbContext, JobTypeDescConfigModel src)
        {
            ArgumentNullException.ThrowIfNull(dbContext, nameof(dbContext));
            ArgumentNullException.ThrowIfNull(src, nameof(src));

            var dest = await dbContext.JobTypeDescConfigsDbSet.FindAsync(src.Id);

            if (dest == null)
            {
                var entry = await dbContext
                     .JobTypeDescConfigsDbSet
                     .AddAsync(src);
                dest = entry.Entity;
            }
            else
            {
                await dbContext.LoadAsync(dest);
            }

            dest.Id = src.Id;
            dest.Name = src.Name;
            dest.FullName = src.FullName;
            dest.OptionEditors = src.OptionEditors;
            dest.Description = src.Description;
            return dest;
        }

    }
}
