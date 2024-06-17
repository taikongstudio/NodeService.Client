using System.Collections.Immutable;

namespace NodeService.ServiceHost.Tasks
{
    public abstract class TaskBase
    {
        public ILogger Logger { get; private set; }

        public TaskDefinitionModel TaskDefinition { get; private set; }

        public ApiService ApiService { get; private set; }

        public ImmutableDictionary<string, string?> EnvironmentVariables { get; private set; }

        protected TaskBase(ApiService apiService, ILogger<TaskBase> logger)
        {
            ApiService = apiService;
            Logger = logger;
            EnvironmentVariables = ImmutableDictionary<string, string?>.Empty;
        }

        public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);

        public void SetTaskDefinition(TaskDefinitionModel taskDefinition)
        {
            TaskDefinition = taskDefinition;
        }

        public void SetEnvironmentVariables(IEnumerable<StringEntry> envVars)
        {
            if (envVars == null)
            {
                this.EnvironmentVariables = ImmutableDictionary<string, string?>.Empty;
            }
            else
            {
                var builder = ImmutableDictionary.CreateBuilder<string, string?>();
                foreach (var item in envVars)
                {
                    if (string.IsNullOrEmpty(item.Name))
                    {
                        continue;
                    }
                    builder.Add(KeyValuePair.Create(item.Name, item.Value));
                }
                this.EnvironmentVariables = builder.ToImmutable();
            }

        }

    }
}
