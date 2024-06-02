using System.Runtime.InteropServices;

namespace NodeService.ServiceProcess
{
    public class PackageDatabase : IDisposable
    {

        private readonly SafeHandle _fileHandle;

        private PackageDatabase(SafeHandle safeHandle, string packagePath)
        {
            _fileHandle = safeHandle;
            FullName = packagePath;
        }

        public string FullName { get; private set; }

        public static bool TryOpen(string packagePath, out PackageDatabase? packageDatabase)
        {
            packageDatabase = null;
            try
            {
                if (!Directory.Exists(packagePath))
                {
                    Directory.CreateDirectory(packagePath);
                }
                var lockFilePath = Path.Combine(packagePath, ".lock");
                var handle = File.OpenHandle(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    FileOptions.None);
                packageDatabase = new PackageDatabase(handle, packagePath);
                return true;
            }
            catch (Exception ex)
            {

            }
            return false;
        }

        public void Dispose()
        {
            if (this._fileHandle != null)
            {
                this._fileHandle.Close();
            }
        }

    }
}
