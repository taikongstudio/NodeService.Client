using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

public class ServiceProcessInstallContext
{
    public ServiceProcessInstallContext(string serviceName, string displayName, string description, string installDirectory)
    {
        ServiceName = serviceName;
        DisplayName = displayName;
        Description = description;
        InstallDirectory = installDirectory;
    }

    public string InstallDirectory { get; set; }

    public string ServiceName { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

}