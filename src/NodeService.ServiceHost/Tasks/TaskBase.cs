﻿using Microsoft.Extensions.Hosting;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NodeService.ServiceHost.Tasks
{
    public abstract class TaskBase
    {
        public ILogger Logger { get; private set; }

        public JobScheduleConfigModel JobScheduleConfig { get; private set; }

        public ApiService ApiService { get; private set; }

        protected TaskBase(ApiService apiService, ILogger<TaskBase> logger)
        {
            ApiService = apiService;
            Logger = logger;
        }

        public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);

        public void SetJobScheduleConfig(JobScheduleConfigModel jobScheduleConfig)
        {
            JobScheduleConfig = jobScheduleConfig;
        }

    }

}