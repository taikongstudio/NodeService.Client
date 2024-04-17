using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class PackageUpdateConfig
{
    public string HttpAddress { get; set; }

    public string InstallDirectory { get; set; }

    public string ServiceName { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public int DurationMinutes { get; set; }
}