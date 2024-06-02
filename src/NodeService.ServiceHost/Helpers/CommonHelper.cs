namespace NodeService.ServiceHost.Helpers
{
    public static class CommonHelper
    {
        public static List<ProcessInfo> CollectProcessList(ILogger logger)
        {
            List<ProcessInfo> processInfoList = new List<ProcessInfo>();
            try
            {
                var processes = Process.GetProcesses();
                Dictionary<string, int> errors = new Dictionary<string, int>();
                foreach (var process in processes)
                {
                    try
                    {
                        ProcessInfo processInfo = new ProcessInfo()
                        {
                            FileName = process.ProcessName,
                            ProcessName = process.MainModule.FileName,
                            Id = process.Id,
                            Responding = process.Responding,
                            StartTime = process.StartTime.ToString(NodePropertyModel.DateTimeFormatString),
                            ExitTime = process.HasExited ? process.ExitTime.ToString(NodePropertyModel.DateTimeFormatString) : null,
                            VirtualMemorySize64 = process.VirtualMemorySize64,
                            PagedMemorySize64 = process.PagedMemorySize64,
                            NonpagedSystemMemorySize64 = process.NonpagedSystemMemorySize64,
                            PagedSystemMemorySize64 = process.PagedSystemMemorySize64,
                            HandleCount = process.HandleCount,
                        };
                        processInfoList.Add(processInfo);
                    }
                    catch (Exception ex)
                    {
                        var errorString = ex.ToString();
                        if (!errors.ContainsKey(errorString))
                        {
                            errors.Add(errorString, 1);
                        }
                        else
                        {
                            errors[errorString]++;
                        }

                    }
                }

                foreach (var item in errors)
                {
                    //logger?.LogError($"Count:{item.Value} Exception:{item.Key}");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.ToString());
            }
            return processInfoList;
        }

    }
}
