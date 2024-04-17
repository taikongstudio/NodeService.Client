using Python.Deployment;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class PythonRuntimeService : BackgroundService
    {
        private readonly ILogger<PythonRuntimeService> _logger;

        public PythonRuntimeService(ILogger<PythonRuntimeService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (!await InstallPythonPackageAsync())
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        private void LogMessage(string message)
        {
            _logger.LogInformation(message);
        }

        private async Task<bool> InstallPythonPackageAsync()
        {
            try
            {
                Installer.Source = new Installer.EmbeddedResourceInstallationSource()
                {
                    Assembly = typeof(Job).Assembly,
                    Force = true,
                    ResourceName = "python-3.8.5-embed-amd64.zip"
                };
                string pythonDirectory = "python-3.8.5-embed-amd64";
                if (Directory.Exists(pythonDirectory))
                {
                    Directory.Delete(pythonDirectory, true);
                }
                // install in local directory. if you don't set it will install in local app data of your user account
                Installer.InstallPath = Path.GetFullPath(AppContext.BaseDirectory);

                // see what the installer is doing
                Installer.LogMessage += LogMessage;


                // install from the given source
                await Installer.SetupPython(true);

                // ok, now use pythonnet from that installation
                PythonEngine.Initialize();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

    }
}
