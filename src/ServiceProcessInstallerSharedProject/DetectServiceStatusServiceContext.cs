using System;
using System.Collections.Generic;
using System.Text;

public class DetectServiceStatusServiceContext
{
    public DetectServiceStatusServiceContext()
    {
        this.ServiceRecoveries = new Dictionary<string, Func<string, Task<bool>>>();
    }

    public Dictionary<string, Func<string, Task<bool>>> ServiceRecoveries { get; private set; } = [];

}
