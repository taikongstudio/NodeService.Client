using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System.IO.Compression;

namespace NodeService.WindowsService.Services
{
    internal class UploadMaccorDataJob : Job
    {
        private class MaccorUploadProcessInfo
        {
            public Process Process { get; set; }

            public UploadMaccorDataJobOptions Options { get; set; }

        }


        private readonly Dictionary<string, MaccorUploadProcessInfo> _processList = [];
        private bool disposedValue;
        private string _maccorUploadToolPath;
        private string _arguments;

        public UploadMaccorDataJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {
        }

        private void EnsureProcessInfo(string key, UploadMaccorDataJobOptions options)
        {
            try
            {
                if (!_processList.TryGetValue(key, out var processInfo))
                {
                    processInfo = RunWorker(options);
                    _processList.Add(key, processInfo);
                }
                else if (JsonSerializer.Serialize(options) != JsonSerializer.Serialize(processInfo.Options))
                {
                    processInfo.Process.Kill();
                    var newProcessInfo = RunWorker(options);
                    processInfo.Process = newProcessInfo.Process;
                    processInfo.Options = options;
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

        private MaccorUploadProcessInfo RunWorker(UploadMaccorDataJobOptions options)
        {

            MaccorUploadProcessInfo processInfo = new MaccorUploadProcessInfo();

            processInfo.Process = Process.Start(Path.Combine());
            return processInfo;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~UploadMaccorDataJob()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                UploadMaccorDataJobOptions options = new UploadMaccorDataJobOptions();
                await options.InitAsync(this.JobScheduleConfig, ApiService);

                if (string.IsNullOrEmpty(options.Directory))
                {
                    foreach (var driveInfo in DriveInfo.GetDrives())
                    {
                        options.Directory = Path.Combine(driveInfo.RootDirectory.FullName, "Maccor", "System");
                        Logger.LogInformation($"try use maccor path:{options.Directory}");
                        if (Directory.Exists(options.Directory))
                        {
                            break;
                        }
                    }
                }
                if (!Directory.Exists(options.Directory))
                {
                    Logger.LogError($"Could not found Maccor path:{options.Directory}");
                    return;
                }

                var packageDir = PackageDirectory.GetPackageDirectory(options.PackageConfig);
                await DownloadPackageAsync(options.PackageConfig, packageDir);
                _maccorUploadToolPath = PackageDirectory.GetPackageEntryPoint(options.PackageConfig);
                _arguments = options.PackageConfig.Arguments;

                DirectoryInfo directoryInfo = directoryInfo = new DirectoryInfo(options.Directory);


                while (!stoppingToken.IsCancellationRequested)
                {
                    foreach (var dir in directoryInfo.GetDirectories())
                    {
                        var dataPath = Path.Combine(dir.FullName, "Archive");
                        if (!Directory.Exists(dataPath))
                        {
                            Logger.LogError($"Could not found directory:{dataPath}");
                            continue;
                        }
                        UploadMaccorDataJobOptions uploadMaccorDataJobOptions = new UploadMaccorDataJobOptions();
                        uploadMaccorDataJobOptions = JsonSerializer.Deserialize<UploadMaccorDataJobOptions>(
                            JsonSerializer.Serialize(options));
                        options.Directory = dataPath;

                        EnsureProcessInfo(dataPath, uploadMaccorDataJobOptions);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }



            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        private async Task<bool> DownloadPackageAsync(PackageConfigModel packageConfig, string targetDirectory)
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri("");
                ApiService apiService = new ApiService(httpClient);
                var tempDownloadPath = Path.Combine(Path.GetTempPath(), "NodeService.WindowsService", Guid.NewGuid().ToString());
                var fileStream = new FileStream(tempDownloadPath, FileMode.OpenOrCreate);
                await apiService.DownloadPackageAsync(packageConfig, fileStream);
                fileStream.Position = 0;
                ZipFile.ExtractToDirectory(fileStream, targetDirectory);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            return false;
        }
    }

}
