
using JobsWorker.Shared.MessageQueues;
using JobsWorkerNodeService.Models;

namespace JobsWorkerNodeService.Services
{
    public class JobExecutionService : BackgroundService
    {
        private readonly IInprocMessageQueue<string, string, JobExecutionTriggerEvent> _jobExecutionTriggerEvent;
        public JobExecutionService(IInprocMessageQueue<string, string, JobExecutionTriggerEvent> jobExecutionTriggerEvent)
        {

            this._jobExecutionTriggerEvent = jobExecutionTriggerEvent;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var item in
                this._jobExecutionTriggerEvent
                .ReadAllMessageAsync<JobExecutionTriggerEvent>(nameof(JobExecutionService)))
            {
                Task task = new Task(RunJob, item.Content, stoppingToken, TaskCreationOptions.LongRunning);
                task.Start();
            }
        }

        private void RunJob(object? state)
        {
            JobExecutionTriggerDetails? jobExecutionTriggerDetails = state as JobExecutionTriggerDetails;
            if (jobExecutionTriggerDetails == null)
            {
                return;
            }



        }
    }
}
