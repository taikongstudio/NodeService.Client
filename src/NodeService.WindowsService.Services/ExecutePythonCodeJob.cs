﻿using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using Python.Deployment;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class ExecutePythonCodeJob : Job
    {
        public ExecutePythonCodeJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {
        }

        private void LogPythonMessage(string message)
        {
            Logger.LogInformation(message);
        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ExecutePythonCodeJobOptions options = new();
            await options.InitAsync(this.JobScheduleConfig, ApiService);
            if (options.Code == null)
            {
                Logger.LogError("no code");
                return;
            }



            // call Python's sys.version to prove we are executing the right version
            dynamic sys = Py.Import("sys");
            Console.WriteLine("### Python version:\n\t" + sys.version);

            PythonEngine.Exec(options.Code);
            // This calls my.py's Py_Write(string)
            //			test.Py_Write("csharp to ip");
        }
    }
}