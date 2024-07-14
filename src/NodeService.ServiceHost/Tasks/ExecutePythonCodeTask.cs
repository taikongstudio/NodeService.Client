using Python.Runtime;

namespace NodeService.ServiceHost.Tasks
{
    public class ExecutePythonCodeTask : TaskBase
    {
        public ExecutePythonCodeTask(
            ApiService apiService,
            ILogger<TaskBase> logger) : base(apiService, logger)
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


            dynamic sys = Py.Import("sys");
            Logger.LogInformation($"### Python version:{Environment.NewLine}{sys.version}");

            using var scope = Py.CreateScope();
            scope.Exec(
                """
                import sys

                class NetConsole(object):
                    def __init__(self, writeCallback):
                        self.terminal = sys.stdout
                        self.writeCallback = writeCallback

                    def write(self, message):
                        self.terminal.write(message)
                        self.writeCallback(message)

                    def flush(self):
                        # this flush method is needed for python 3 compatibility.
                        # this handles the flush command by doing nothing.
                        # you might want to specify some extra behavior here.
                        pass

                def setConsoleOut(writeCallback):
                    sys.stdout = NetConsole(writeCallback)
                """
                );

            dynamic setConsoleOutFn = scope.Get("setConsoleOut");

            setConsoleOutFn(new Action<string>(Print));

            scope.Exec(options.Code);
        }

        void Print(string message)
        {
            if (message != Environment.NewLine)
                Logger.LogInformation("[Py]: ");

            Logger.LogInformation(message);
        }
    }
}
