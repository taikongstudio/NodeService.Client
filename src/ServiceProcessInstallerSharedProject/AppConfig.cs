using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class AppConfig
{
    public IEnumerable<PackageUpdateConfig> PackageUpdates { get; set; } = [];

}