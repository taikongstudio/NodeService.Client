using JobsWorker.Shared.Models;
using JobsWorkerWebService.Models;
using Microsoft.EntityFrameworkCore;

namespace JobsWorkerWebService.Data
{
    public class ApplicationProfileDbContext : DbContext
    {
        public DbSet<MachineInfo> MachineInfoDbSet { get; set; }


        public ApplicationProfileDbContext(DbContextOptions<ApplicationProfileDbContext> contextOptions)
            : base(contextOptions)
        {
           
        }

        protected ApplicationProfileDbContext(DbContextOptions contextOptions)
            : base(contextOptions)
        {

        }
    }
}
