using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using NodeService.DeviceHost.Data.Models;
using NodeService.Infrastructure.Data;
using NodeService.Infrastructure.DataModels;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.DeviceHost.Data
{
    public partial class ApplicationDbContext : DbContext
    {
        public DatabaseProviderType ProviderType { get; }

        public DbSet<ChongQingYinHeDataModel> ChongQingYinHeDataDbSet { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            foreach (var extension in options.Extensions)
            {
                if (!extension.Info.IsDatabaseProvider) continue;
                if (extension is MySqlOptionsExtension)
                    ProviderType = DatabaseProviderType.MySql;
                else if (extension is SqlServerOptionsExtension)
                    ProviderType = DatabaseProviderType.SqlServer;
                else if (extension is SqliteOptionsExtension)
                    ProviderType = DatabaseProviderType.Sqlite;
            }
        }


        protected ApplicationDbContext(DbContextOptions contextOptions)
            : base(contextOptions)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ChongQingYinHeDataModel>().HasKey(t => t.Id);
        }

    }
}
