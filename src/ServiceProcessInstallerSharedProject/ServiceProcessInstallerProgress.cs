using System;
using System.Collections.Generic;
using System.Text;

public enum ServiceProcessInstallerProgressType
{
    Info,
    Warning,
    Error
}

public class ServiceProcessInstallerProgress
{
    public ServiceProcessInstallerProgress(
        string serviceName,
        ServiceProcessInstallerProgressType type,
        string message)
    {
        ServiceName = serviceName;
        Type = type;
        Message = message;
    }

    public string Message { get; private set; }

    public string ServiceName { get; private set; }

    public ServiceProcessInstallerProgressType Type { get; private set; }
}
