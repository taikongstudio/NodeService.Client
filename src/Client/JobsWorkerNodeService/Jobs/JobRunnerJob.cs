using JobsWorker.Shared.Models;
using Quartz;

namespace JobsWorkerNodeService.Jobs
{
    public class JobRunnerJob : IJob
    {
        public IDictionary<string, object> Options { get; set; }

        public string JobsId { get; set; }

        public string JobName { get; set; }

        public string JobType { get; set; }

        public Task Execute(IJobExecutionContext context)
        {

            return Task.CompletedTask;
        }
    }
}
