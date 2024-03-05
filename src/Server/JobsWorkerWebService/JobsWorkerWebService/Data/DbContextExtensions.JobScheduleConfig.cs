using JobsWorker.Shared.DataModels;
using System.Linq.Expressions;

namespace JobsWorkerWebService.Data
{
    public  static partial class DbContextExtensions
    {
        public static async Task<JobScheduleConfigModel> LoadAsync(this ApplicationDbContext dbContext, JobScheduleConfigModel model)
        {
            var entry = dbContext.Entry(model);

            await entry.Collection(x => x.TemplateBindingList).LoadAsync();

            await entry.Collection(x => x.NodeInfoJobScheduleConfigBindingList).LoadAsync();

            await entry.Collection(x => x.JobExecutionRecords).LoadAsync();


            await entry.Collection(x => x.Templates).LoadAsync();

            await entry.Navigation(nameof(JobScheduleConfigModel.JobTypeDesc)).LoadAsync();

            return model;
        }

        public static async Task<JobScheduleConfigModel> AddOrUpdateAsync(this ApplicationDbContext dbContext, JobScheduleConfigModel src)
        {
            ArgumentNullException.ThrowIfNull(dbContext, nameof(dbContext));
            ArgumentNullException.ThrowIfNull(src, nameof(src));

            var dest = await dbContext.JobScheduleConfigsDbSet.FindAsync(src.Id);

            if (dest == null)
            {
                var entry = await dbContext
                     .JobScheduleConfigsDbSet
                     .AddAsync(src);
                dest = entry.Entity;
            }
            else
            {
                await dbContext.LoadAsync(dest);
            }

            dest.Id = src.Id;
            dest.Name = src.Name;
            dest.CronExpressions = src.CronExpressions;
            dest.Description = src.Description;
            dest.Options = src.Options;
            dest.DnsFilters = src.DnsFilters;
            dest.DnsFilterType = src.DnsFilterType;
            dest.IpAddressFilters = src.IpAddressFilters;
            dest.IpAddressFilterType = src.IpAddressFilterType;
            dest.IsEnabled = src.IsEnabled;
            dest.JobTypeDescForeignKey = src.JobTypeDescForeignKey;

            return dest;
        }

    }
}
