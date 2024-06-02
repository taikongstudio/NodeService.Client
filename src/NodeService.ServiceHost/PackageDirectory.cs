namespace NodeService.ServiceHost
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
