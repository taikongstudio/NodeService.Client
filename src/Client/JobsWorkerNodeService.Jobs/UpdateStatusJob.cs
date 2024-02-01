using FluentFTP;
using JobsWorker.Shared.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace JobsWorkerNodeService.Jobs
{
    public class UpdateStatusJob : JobBase
    {




        public override async Task Execute(IJobExecutionContext context)
        {
            try
            {
                this.Logger.LogInformation($"Begin update");


            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

    }
}
