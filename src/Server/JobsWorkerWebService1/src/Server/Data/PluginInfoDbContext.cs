using JobsWorker.Shared.Models;
using JobsWorkerWebService.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace JobsWorkerWebService.Server.Data
{
    public class PluginInfoDbContext: DbContext
    {
        public DbSet<PluginInfo> PluginInfoDbSet { get; set; }

        public PluginInfoDbContext(DbContextOptions<PluginInfoDbContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

    }
}
