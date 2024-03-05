using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace JobsWorkerWebService.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseMySql("server=10.201.76.20;userid=root;password=XWd@2024IT;database=jobs_worker_current;",

                MySqlServerVersion.LatestSupportedServerVersion, mySqlOptionBuilder =>
                {
                    mySqlOptionBuilder.EnableStringComparisonTranslations();
                });

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
