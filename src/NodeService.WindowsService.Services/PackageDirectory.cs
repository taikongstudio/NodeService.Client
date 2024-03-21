using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class PackageDirectory
    {
        public const string PackagesDirectory = "Packages";

        public static string GetPackageDirectory(PackageConfigModel packageConfig)
        {
            return Path.Combine(AppContext.BaseDirectory, PackagesDirectory, packageConfig.Version);
        }

        public static string GetPackageEntryPoint(PackageConfigModel packageConfig)
        {
            return Path.Combine(AppContext.BaseDirectory, PackagesDirectory, packageConfig.Version, packageConfig.EntryPoint);
        }


    }
}
