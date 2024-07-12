using Python.Runtime;

namespace NodeService.ServiceHost.Tasks
{
    public class ExecutePythonCodeTask : TaskBase
    {
        public ExecutePythonCodeTask(ApiService apiService, ILogger<TaskBase> logger) : base(apiService, logger)
        {
        }

        private void LogPythonMessage(string message)
        {
            Logger.LogInformation(message);
        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = new ExecutePythonCodeTaskOptions();
            await options.InitAsync(TaskDefinition, ApiService, stoppingToken);
            if (options.Code == null)
            {
                Logger.LogError("no code");
                return;
            }
            PythonEngine.Initialize();
            // call Python's sys.version to prove we are executing the right version
            dynamic sys = Py.Import("sys");
            Console.WriteLine("### Python version:\n\t" + sys.version);
            using (Py.GIL())
            {
                PythonEngine.Exec(options.Code);
                // This calls my.py's Py_Write(string)
                //			test.Py_Write("csharp to ip");
            }
        }
    }
}
