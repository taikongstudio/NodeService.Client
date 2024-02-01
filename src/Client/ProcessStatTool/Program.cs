using CommandLine;
using FluentFTP;
using FluentFTP.Helpers;
using Org.BouncyCastle.Asn1;
using ProcessStatTool.Helpers;
using ProcessStatTool.Models;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace ProcessStatTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Parser.Default.ParseArguments<Options>(args)
             .WithParsed((options) =>
             {
                 if (Debugger.IsAttached)
                 {
                     options.rsp = Path.Combine(AppContext.BaseDirectory, "config/updateconfig_bl.json");
                 }
                 if (options.rsp != null)
                 {
                     using var fileStream = File.OpenRead(options.rsp);
                     var config = JsonSerializer.Deserialize<config>(fileStream);
                     options.host = config.host;
                     options.port = config.port;
                     options.username = config.username;
                     options.password = config.password;
                     options.action = config.action;
                     options.args = config.args;
                 }
                 RunWithOptions(options, args);
             });
        }

        private static void RunWithOptions(Options options, string[] args)
        {
            switch (options.action)
            {
                case "usages":
                    AnalysisUsages(options);
                    break;
                case "updateallmachineusages":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var initonlyString = options.args.Where(x => x.StartsWith("initonly=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(initonlyString))
                        {
                            initonlyString = "false";
                        }
                        var filterTypeString = options.args.Where(x => x.StartsWith("filtertype=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        var hostNamesString = options.args.Where(x => x.StartsWith("hostnames=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        string[] hostNames = Array.Empty<string>();
                        if (hostNamesString != null)
                        {
                            hostNames = hostNamesString.Split(",", StringSplitOptions.RemoveEmptyEntries);
                        }
                        if (!hostNames.Any() || string.IsNullOrEmpty(filterTypeString))
                        {
                            filterTypeString = "none";
                        }
                        var configrootString = options.args.Where(x => x.StartsWith("configroot=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(configrootString))
                        {
                            configrootString = AppContext.BaseDirectory;
                        }
                        var channelString = options.args.Where(x => x.StartsWith("channel=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(channelString))
                        {
                            channelString = "beta";
                        }

                        var deleteOnlyString = options.args.Where(x => x.StartsWith("deleteonly=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(deleteOnlyString))
                        {
                            channelString = "true";
                        }
                        AnalysisAllMachineUsages(options, mySqlConfig);
                    }
                    break;
                default:
                    break;
            }
        }

        private class TaskParameters
        {
            public Options Options { get; set; }

            public FtpClient? FtpClient { get; set; }

            public List<UsagesMapping> UsagesMappings { get; set; }

            public MachineInfo MachineInfo { get; set; }

            public string NodeName { get; set; }

            public ConcurrentQueue<FtpClient> FtpClientsPool { get; set; }

            public object Lock { get; set; }

            public List<Cache> CacheList { get; set; }
        }

        private class Cache
        {
            public string NodeName { get; set; }

            public string Usages { get; set; }
        }

        private static void AnalysisAllMachineUsages(Options options, MySqlConfig mySqlConfig)
        {
            using var mainClient = new FtpClient(options.host, options.username, options.password, options.port, new FtpConfig()
            {
                ConnectTimeout = 60000,
                DataConnectionConnectTimeout = 60000,
                DataConnectionReadTimeout = 60000
            });
            mainClient.AutoConnect();
            object lockObj = new object();
            var machineList = SqlHelper.GetMachineInfoList(mySqlConfig, Console.WriteLine);
            var usagesMapping = DownloadUsagesMappings(mainClient);
            ConcurrentQueue<FtpClient> ftpClientsPool = new ConcurrentQueue<FtpClient>();
            List<TaskParameters> tasks = new List<TaskParameters>();
            var cacheList = !File.Exists("cache.txt") ? new List<Cache>() : JsonSerializer.Deserialize<List<Cache>>(File.ReadAllText("cache.txt"));
            foreach (var machineDir in mainClient.GetListing("/JobsWorkerDaemonService/"))
            {
                if (machineDir.Type == FtpObjectType.File)
                {
                    continue;
                }
                var nodeInfo = machineList.FirstOrDefault(x => string.Equals(machineDir.Name, x.computer_name, StringComparison.OrdinalIgnoreCase));
                if (nodeInfo == null)
                {
                    Console.WriteLine($"missing machine info:{machineDir.Name}");
                    continue;
                }

                var cache = cacheList.FirstOrDefault(x => string.Equals(x.NodeName, machineDir.Name, StringComparison.OrdinalIgnoreCase));
                if (cache != null)
                {
                    nodeInfo.usages = cache.Usages;
                    continue;
                }

                nodeInfo.has_ftp_dir = true;
                SqlHelper.InsertOrUpdate(mySqlConfig, new MachineInfo[] { nodeInfo }, Console.WriteLine).Wait();

                TaskParameters taskParameters = new TaskParameters();
                taskParameters.Options = options.Clone();
                taskParameters.Options.input = Path.Combine(machineDir.FullName, "processlist");
                taskParameters.UsagesMappings = usagesMapping;
                taskParameters.MachineInfo = nodeInfo;
                taskParameters.NodeName = machineDir.Name;
                taskParameters.FtpClientsPool = ftpClientsPool;
                taskParameters.Lock = new object();
                taskParameters.CacheList = cacheList;
                tasks.Add(taskParameters);
            }
            Parallel.ForEach(tasks, new ParallelOptions()
            {
                MaxDegreeOfParallelism = 4
            }, RunTask);

            foreach (var ftpClient in ftpClientsPool)
            {
                ftpClient.Dispose();
            }

            SqlHelper.InsertOrUpdate(mySqlConfig, machineList, Console.WriteLine).Wait();

            static void RunTask(TaskParameters taskParameters)
            {
                try
                {
                    Console.WriteLine($"Processing {taskParameters.NodeName}");
                    if (!taskParameters.FtpClientsPool.TryDequeue(out FtpClient ftpClient))
                    {
                        ftpClient = new FtpClient(taskParameters.Options.host,
                            taskParameters.Options.username,
                            taskParameters.Options.password,
                            taskParameters.Options.port);
                        Console.WriteLine("init ftp client");
                    }
                    else
                    {
                        Console.WriteLine("Get ftp client from pool");
                    }
                    taskParameters.FtpClient = ftpClient;
                    if (!taskParameters.FtpClient.DirectoryExists(taskParameters.Options.input))
                    {
                        Console.WriteLine($"missing {taskParameters.Options.input}");
                        return;
                    }
                    var usages = AnalysisMachineUsagesImpl(taskParameters.Options, taskParameters.FtpClient, taskParameters.UsagesMappings);
                    if (string.IsNullOrEmpty(usages))
                    {
                        Console.WriteLine($"{taskParameters.NodeName} no usages");
                        return;
                    }
                    Console.Write($"{taskParameters.NodeName}  set {taskParameters.MachineInfo.usages} to ");
                    taskParameters.MachineInfo.usages = MergeUsages(taskParameters.MachineInfo.usages, usages);
                    Console.Write(taskParameters.MachineInfo.usages);
                    Console.WriteLine();
                    lock (taskParameters.Lock)
                    {
                        var cacheList = taskParameters.CacheList;
                        var cache = cacheList.FirstOrDefault(x => x.NodeName == taskParameters.NodeName);
                        if (cache == null)
                        {
                            cache = new Cache()
                            {
                                NodeName = taskParameters.NodeName,
                                Usages = taskParameters.MachineInfo.usages
                            };
                            cacheList.Add(cache);
                        }
                        var jsonStr = JsonSerializer.Serialize(cacheList);
                        File.WriteAllText("cache.txt", jsonStr);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    if (taskParameters.FtpClient != null)
                    {
                        taskParameters.FtpClientsPool.Enqueue(taskParameters.FtpClient);
                        taskParameters.FtpClient = null;
                    }
                }

            }

        }
        



        private static string MergeUsages(string oldUsages, string newUsages)
        {
            var newList = newUsages.Split(',').ToList();
            List<string> oldList = new List<string>();
            if (oldUsages != null)
            {
                oldList = oldUsages.Replace('.', ',').Replace('，', ',').Replace('、', ',').Split(',').ToList();
            }
            var unionList = oldList.Union(newList).Distinct().ToList();
            unionList.Sort();
            return string.Join(",", unionList);
        }

        private static List<UsagesMapping> DownloadUsagesMappings(FtpClient ftpClient)
        {
            List<UsagesMapping> usagesMappings = new List<UsagesMapping>();
            {
                using var stream = new MemoryStream();
                if (!ftpClient.DownloadStream(stream, "/FtpStatToolConfig/machine_usages_mapping.json"))
                {
                    //Console.WriteLine("Download /FtpStatToolConfig/machine_usages_mapping.json failed");
                    return new List<UsagesMapping>();
                }
                stream.Position = 0;
                usagesMappings = JsonSerializer.Deserialize<List<UsagesMapping>>(stream);
            }
            return usagesMappings;
        }

        private static void AnalysisUsages(Options options)
        {
            using var ftpClient = new FtpClient(options.host, options.username, options.password, options.port);
            ftpClient.AutoConnect();

            foreach (var item in ftpClient.GetListing("/JobsWorkerDaemonService/"))
            {
                options.input = Path.Combine(item.FullName, "processlist");
                if (!ftpClient.DirectoryExists(options.input))
                {
                    Console.WriteLine($"Skip {item.FullName}");
                    continue;
                }
                Console.WriteLine($"Processing {item.FullName}");
                AnalysisMachineUsagesImpl(options, ftpClient, DownloadUsagesMappings(ftpClient));
            }

        }

        private static string AnalysisMachineUsagesImpl(Options options, FtpClient ftpClient, List<UsagesMapping> usagesMappings)
        {
            ProcessStatAnalysisResult processStatAnalysisResult = new ProcessStatAnalysisResult();
            processStatAnalysisResult.Entries = new List<Entry>();
            try
            {


                ftpClient.AutoConnect();

                var processStatFiles = ftpClient.GetListing(options.input);
                var hashSet = new HashSet<(string FileName, string ProcessName)>();
                foreach (var processListFileItem in processStatFiles)
                {
                    try
                    {
                        using var stream = new MemoryStream((int)processListFileItem.Size);
                        if (!ftpClient.DownloadStream(stream, processListFileItem.FullName))
                        {
                            continue;
                        }
                        //Console.WriteLine($"Downloading {processListFileItem.FullName}");
                        stream.Position = 0;
                        var processStatInfoArray = JsonSerializer.Deserialize<ProcessStatInfo[]>(stream);
                        foreach (var processStatInfo in processStatInfoArray)
                        {
                            hashSet.Add((processStatInfo.FileName, processStatInfo.ProcessName));
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
                List<string> usagesList = new List<string>();
                foreach (var usagesMapping in usagesMappings)
                {
                    foreach (var item in hashSet)
                    {

                        if (item.ProcessName.Contains(usagesMapping.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!usagesList.Contains(usagesMapping.Name))
                            {
                                usagesList.Add(usagesMapping.Name);
                                processStatAnalysisResult.Entries.Add(new Entry()
                                {
                                    Exists = true,
                                    Name = usagesMapping.Name,
                                });
                            }
                        }
                    }
                }
                var usages = string.Join(",", usagesList);
                //Console.WriteLine(string.Join(",", usagesList));
                return usages;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                try
                {
                    using (var fileStream = File.OpenWrite(options.output))
                    {
                        JsonSerializer.Serialize(fileStream, processStatAnalysisResult);
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ex.ToString());
                }
            }
            return string.Empty;
        }

    }
}
