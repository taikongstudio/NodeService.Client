﻿namespace NodeService.ServiceProcess
{
    public static class ServiceProcessInstallerHelper
    {
        public static ServiceProcessInstaller Create(string serviceName,
                                                      string displayName,
                                                      string description,
                                                      string filePath,
                                                      string arguments)
        {
            var serviceProcessInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            InstallContext context = new InstallContext();
            if (filePath != null && filePath.Length > 0)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                string path = $"assemblypath=\"{filePath}\" {arguments}";
                string[] cmdline = arguments == null ? [path] : [path, arguments];
                context = new InstallContext("", cmdline);
            }

            serviceProcessInstaller.Context = context;
            serviceInstaller.ServiceName = serviceName;
            serviceInstaller.DisplayName = displayName;
            serviceInstaller.Description = description;
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.Parent = serviceProcessInstaller;
            return serviceProcessInstaller;
        }

        public static TransactedInstaller CreateTransactedInstaller(string serviceName,
                                              string displayName,
                                              string description,
                                              string filePath,
                                              string arguments)
        {
            var serviceProcessInstaller = Create(serviceName, displayName, description, filePath, arguments);
            var transactedInstaller = new TransactedInstaller();
            serviceProcessInstaller.Parent = transactedInstaller;
            transactedInstaller.Context = serviceProcessInstaller.Context;
            return transactedInstaller;
        }

        public static async IAsyncEnumerable<ServiceProcessInstallerProgress> UninstallAllService(string[] serviceNames)
        {
            for (int i = 0; i < serviceNames.Length; i++)
            {
                var serviceName = serviceNames[i];
                ServiceProcessInstallerProgressType type = ServiceProcessInstallerProgressType.Info;
                string message = string.Empty;
                message = $"开始卸载服务\"{serviceName}\"";
                yield return new ServiceProcessInstallerProgress(serviceName, type, message);
                try
                {
                    using var serviceProcessInstaller = Create(serviceName, null, null, null, null);
                    serviceProcessInstaller.Uninstall(null);
                    message = $"卸载服务\"{serviceName}\"成功";
                }
                catch (InstallException ex)
                {
                    if (ex.InnerException is Win32Exception win32Exception && win32Exception.NativeErrorCode == 1060)
                    {
                        type = ServiceProcessInstallerProgressType.Warning;
                        message = $"卸载服务\"{serviceName}\"失败:{win32Exception.Message}";
                    }
                }
                catch (Exception ex)
                {
                    type = ServiceProcessInstallerProgressType.Error;
                    message = $"卸载服务\"{serviceName}\"失败:{ex.Message}";
                }
                await Task.CompletedTask;
                yield return new ServiceProcessInstallerProgress(serviceName, type, message);
            }
            yield break;
        }
    }
}
