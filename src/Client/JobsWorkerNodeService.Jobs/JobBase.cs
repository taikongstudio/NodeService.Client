using JobsWorker.Shared.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs
{
    public abstract class JobBase : IJob
    {
        public Dictionary<string, string> options { get; set; }

        public ILogger Logger { get; set; }

        public NodeConfig NodeConfig { get; set; }

        public abstract Task Execute(IJobExecutionContext context);


    }
}
