using JobsWorker.Shared.Models;
using JobsWorkerWebService.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace JobsWorkerWebService.Server.Data
{
    public class NodeInfoDbContext: DbContext
    {
        public DbSet<NodeInfo> NodeInfoDbSet { get; set; }

        public NodeInfoDbContext(DbContextOptions<NodeInfoDbContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
    }
}
