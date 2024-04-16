using System;
using System.Collections.Generic;
using System.Text;

public class InstallerProgressEventArgs : EventArgs
{
    public InstallerProgressEventArgs(ServiceProcessInstallerProgress progress)
    {
        Progress = progress;
    }

    public ServiceProcessInstallerProgress Progress { get; private set; }
}
