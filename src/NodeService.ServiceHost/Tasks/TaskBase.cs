namespace NodeService.ServiceHost.Tasks
{
    public abstract class TaskBase
    {
        public ILogger Logger { get; private set; }

        public JobScheduleConfigModel TaskScheduleConfig { get; private set; }

        public ApiService ApiService { get; private set; }

        protected TaskBase(ApiService apiService, ILogger<TaskBase> logger)
        {
            ApiService = apiService;
            Logger = logger;
        }

        public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);

        public void SetTaskScheduleConfig(JobScheduleConfigModel jobScheduleConfig)
        {
            TaskScheduleConfig = jobScheduleConfig;
        }

    }

}
