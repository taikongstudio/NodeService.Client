using JobsWorker.Shared.Models;
using JobsWorkerWebService.Models;
using Microsoft.EntityFrameworkCore;

namespace JobsWorkerWebService.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<NodeConfigInfo> NodeConfigInfoDbSet { get; set; }

        public DbSet<NodeInfo> NodeInfoDbSet { get; set; }

        public DbSet<PluginInfo> PluginInfoDbSet { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> contextOptions)
            : base(contextOptions)
        {
        }

        protected ApplicationDbContext(DbContextOptions contextOptions)
            : base(contextOptions)
        {
        }
    }
}
