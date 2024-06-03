using System.Collections.Immutable;

namespace NodeService.ServiceHost.Tasks
{
    public abstract class TaskBase
    {
        public ILogger Logger { get; private set; }

        public JobScheduleConfigModel TaskScheduleConfig { get; private set; }

        public ApiService ApiService { get; private set; }

        public ImmutableArray<KeyValuePair<string, string?>> EnvironmentVariables { get; private set; } = [];

        protected TaskBase(ApiService apiService, ILogger<TaskBase> logger)
        {
            ApiService = apiService;
            Logger = logger;
        }

        public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);

        public void SetTaskScheduleConfig(JobScheduleConfigModel taskDefinition)
        {
            TaskScheduleConfig = taskDefinition;
        }

        public void SetEnvironmentVariables(IEnumerable<StringEntry> envVars)
        {
            if (envVars == null)
            {
                this.EnvironmentVariables = ImmutableArray<KeyValuePair<string, string?>>.Empty;
            }
            else
            {
                var builder = ImmutableArray.CreateBuilder<KeyValuePair<string, string?>>();
                foreach (var item in envVars)
                {
                    if (string.IsNullOrEmpty(item.Name))
                    {
                        continue;
                    }
                    builder.Add(KeyValuePair.Create(item.Name, item.Value));
                }
                this.EnvironmentVariables = builder.ToImmutableArray();
            }

        }

    }
}
