using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Collections
{
    public class JobExecutionContextDictionary : ConcurrentDictionary<string, JobExecutionContext>
    {

    }
}
