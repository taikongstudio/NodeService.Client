using JobsWorker.Shared.Models;
using JobsWorkerWebService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace JobsWorkerWebService.Server.Data
{
    public class NodeConfigInfoDbContext : DbContext
    {
        public DbSet<NodeConfigInfo> NodeConfigInfoDbSet { get; set; }

        public NodeConfigInfoDbContext(DbContextOptions<NodeConfigInfoDbContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
    }
}
