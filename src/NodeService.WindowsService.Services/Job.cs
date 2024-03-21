using Microsoft.Extensions.Hosting;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NodeService.WindowsService.Services
{
    public abstract class Job
    {
        public ILogger Logger { get; private set; }

        public JobScheduleConfigModel JobScheduleConfig { get; set; }

        public ApiService ApiService { get; private set; }

        protected Job(ApiService apiService, ILogger<Job> logger)
        {
            ApiService = apiService;
            Logger = logger;
        }

        public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);




    }

}
