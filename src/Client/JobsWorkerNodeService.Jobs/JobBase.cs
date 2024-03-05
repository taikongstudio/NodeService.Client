using JobsWorker.Shared.DataModels;
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
        public JobScheduleConfigModel JobScheduleConfig { get; set; }

        public ILogger Logger { get; set; }

        public NodeConfigTemplateModel NodeConfigTemplate { get; set; }

        public abstract Task Execute(IJobExecutionContext context);


    }
}
