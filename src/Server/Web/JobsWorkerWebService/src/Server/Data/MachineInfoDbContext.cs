using JobsWorkerWebService.Server.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace JobsWorkerWebService.Server.Data
{
    public class MachineInfoDbContext: DbContext
    {
        public DbSet<MachineInfo> MachineInfos { get; set; }

        public MachineInfoDbContext(DbContextOptions<MachineInfoDbContext> options) : base(options)
        {

        }
    }
}
