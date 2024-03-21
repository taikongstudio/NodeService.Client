using CommandLine;
using FluentFTP;
using FluentFTP.Helpers;
using ftpservertool.Models;
using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using ZstdSharp.Unsafe;

namespace ftpservertool
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
                     options.rsp = Path.Combine(AppContext.BaseDirectory, "config/updateconfig_gm_test.json");
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
                case "updateconfig":
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

                        UpdateConfigParameters updateConfigParameters = new UpdateConfigParameters();
                        updateConfigParameters.channel = channelString;
                        updateConfigParameters.configRoot = configrootString;
                        updateConfigParameters.initOnly = bool.Parse(initonlyString);
                        updateConfigParameters.hostNameList = hostNames;
                        updateConfigParameters.filterType = filterTypeString;
                        updateConfigParameters.deleteonly = bool.Parse(deleteOnlyString);
                        UpdateConfig(options, mySqlConfig, updateConfigParameters);
                    }

                    break;
                case "randomeupdateconfig":
                    {
                        var countString = options.args.Where(x => x.StartsWith("count=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(countString))
                        {
                            countString = "10";
                        }
                        {
                            using var ftpClient = new FtpClient(options.host, options.username, options.password);
                            ftpClient.AutoConnect();
                            var machineDirs = ftpClient.GetListing("/JobsWorkerDaemonService");
                            int count = int.Parse(countString);
                            HashSet<string> machineList = new HashSet<string>();
                            for (int i = 0; i < count; i++)
                            {
                                int currentCount = machineList.Count;
                                do
                                {
                                    machineList.Add(machineDirs.ElementAt(Random.Shared.Next(0, machineDirs.Length)).Name);
                                } while (currentCount == machineList.Count);

                            }
                            int index = 0;
                            var argsCopy = options.args.ToArray();
                            foreach (var item in argsCopy)
                            {
                                if (item.StartsWith("hostnames"))
                                {
                                    argsCopy[index] = $"hostnames={string.Join(",", machineList)}";
                                }
                                index++;
                            }
                            options.args = argsCopy;
                        }

                        goto case "updateconfig";
                    }
                    break;
                case "checklogdate":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        CheckLogDate(options, mySqlConfig);
                    }

                    break;
                case "checklogexception":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var viewsummaryString = options.args.Where(x => x.StartsWith("viewsummary=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(viewsummaryString))
                        {
                            viewsummaryString = "false";
                        }
                        var outputfilename = options.args.Where(x => x.StartsWith("outputfile=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();

                        CheckLogException(options, bool.Parse(viewsummaryString), outputfilename);
                    }
                    break;
                case "checkupdate":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var datetimeString = options.args.Where(x => x.StartsWith("datetime=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        var keywords = options.args.Where(x => x.StartsWith("keywords=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(datetimeString))
                        {
                            datetimeString = DateTime.Now.AddDays(-1).ToString();
                        }
                        if (string.IsNullOrEmpty(keywords))
                        {
                            Console.WriteLine("versionGuidString is null");
                            return;
                        }
                        CheckUpdate(options, mySqlConfig, DateTime.Parse(datetimeString), keywords);
                    }

                    break;
                case "deletelogscache":
                    DeleteLogsCache(options);
                    break;
                case "updatemachineinfo":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var csvFilePathString = options.args.Where(x => x.StartsWith("csv=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        UpdateMachineInfo(options, mySqlConfig, csvFilePathString);
                    }

                    break;
                case "genmachineinfostatus":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var csvoutFilePathString = options.args.Where(x => x.StartsWith("csvout=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        var csvFilePathString = options.args.Where(x => x.StartsWith("csv=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        var channelString = options.args.Where(x => x.StartsWith("channel=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(channelString))
                        {
                            channelString = "beta";
                        }
                        GenMachineInfoStatus(options, mySqlConfig, csvFilePathString, csvoutFilePathString, channelString);
                    }

                    break;
                case "genmaccorstatus":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var csvFilePathString = options.args.Where(x => x.StartsWith("csv=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        var csvoutFilePathString = options.args.Where(x => x.StartsWith("csvout=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();

                        GenMarccorStatus(options, mySqlConfig, csvFilePathString, csvoutFilePathString);
                    }
                    break;
                case "checkconfigdir":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var channelString = options.args.Where(x => x.StartsWith("channel=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(channelString))
                        {
                            channelString = "beta";
                        }
                        CheckConfigDir(options, mySqlConfig, channelString);
                    }

                    break;
                case "findunknownmachine":
                    {
                        List<MySqlConfig> mySqlConfigs = new List<MySqlConfig>();
                        List<FtpConfig> ftpConfigs = new List<FtpConfig>();
                        {
                            int index = 0;
                            while (options.TryReadFtpConfigWithIndex(index, out var ftpConfig))
                            {
                                ftpConfigs.Add(ftpConfig);
                                ftpConfig.Index = index;
                                index++;
                            }
                        }

                        {
                            int index = 0;
                            while (options.TryReadMySqlConfigWithIndex(index, out var mySqlConfig))
                            {
                                mySqlConfigs.Add(mySqlConfig);
                                mySqlConfig.Index = index;
                                index++;
                            }
                        }
                        FindUnkownMachines(mySqlConfigs, ftpConfigs);
                    }
                    break;
                case "mergeext1":
                    {
                        MySqlConfig mySqlConfig = options.ReadMySqlConfig();
                        var channelString = options.args.Where(x => x.StartsWith("channel=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                        if (string.IsNullOrEmpty(channelString))
                        {
                            channelString = "beta";
                        }
                        MergeExt1(mySqlConfig, channelString);
                    }
                    break;
                default:
                    Console.WriteLine($"Unkown action:{options.action}");
                    break;
            }
        }

        private static void MergeExt1(MySqlConfig mySqlConfig, string channel)
        {
            var factoryNameDict = new Dictionary<string, string>()
                {
                    {"bl","博罗" },
                    {"gm","光明" },
                };
            var nodeInfoList = SqlHelper.GetMachineInfoList(mySqlConfig);
            var ext1List = SqlHelper.GetMachineInfoExt1List(mySqlConfig)
                .GroupBy(x => x.computer_name)
                .Select(x => x.FirstOrDefault())
                .ToDictionary(x => x.computer_name);
            foreach (var item in nodeInfoList)
            {
                if (!ext1List.TryGetValue(item.computer_name, out var extInfo1))
                {
                    continue;
                }
                if (extInfo1 != null)
                {
                    extInfo1.Marked = true;
                    if (!item.install_status)
                    {
                        item.remarks = extInfo1.remarks;
                    }
                    item.usages = extInfo1.usages;
                }
            }
            var dataList = nodeInfoList.ToList();
            var factoryName = factoryNameDict[channel];
            var unmarked = ext1List.Where(x => !x.Value.Marked).Select(x => x.Value).ToArray();
            foreach (var item in unmarked)
            {
                if (factoryName == item.factory_name)
                {
                    dataList.Add(item);
                    item.factory_name = string.Empty;
                    item.test_info = string.Empty;
                    item.lab_area = string.Empty;
                    item.lab_name = string.Empty;
                    item.login_name = string.Empty;
                }
            }
            SqlHelper.InsertOrUpdate(mySqlConfig, dataList, Console.WriteLine).Wait();

        }

        class MachineItem
        {
            public int Index { get; set; }

            public string Name { get; set; }
        }

        private static bool CompareDns(string dns1,string dns2)
        {
            return string.Equals(dns1, dns2, StringComparison.OrdinalIgnoreCase);
        }

        private static void FindUnkownMachines(List<MySqlConfig> mySqlConfigs, List<FtpConfig> ftpConfigs)
        {
            List<MachineObject> machineObjects = new List<MachineObject>();
            int index = 0;
            foreach (var ftpConfig in ftpConfigs)
            {

                var ftpClient = new FtpClient(ftpConfig.host, ftpConfig.username, ftpConfig.password, ftpConfig.port);
                if (!ftpClient.DirectoryExists("JobsWorkerDaemonService"))
                {
                    continue;
                }

                var mysqlConfig = mySqlConfigs[index];

                var mysqlRecords = SqlHelper.GetMachineInfoList(mysqlConfig);

                var machineDirectories = ftpClient.GetListing("JobsWorkerDaemonService");

                foreach (var machineDir in machineDirectories)
                {
                    Console.WriteLine($"{ftpClient.Host} Process:{machineDir.FullName}");
                    var configPath = Path.Combine(machineDir.FullName, "config");
                    var logsPath = Path.Combine(machineDir.FullName, "logs");
                    var todaysJobsWorkerLog = Path.Combine(logsPath, "JobWorker-2024-01-19.log");
                    var todaysShouhuLog = Path.Combine(logsPath, "ShuCaiShouHu-2024-01-19.log");

                    MachineObject machineObject = new MachineObject()
                    {
                        FtpClient = ftpClient,
                        HasTodayJobWorkerLogsPath = ftpClient.FileExists(todaysJobsWorkerLog),
                        HasTodayShouhuLogsPath = ftpClient.FileExists(todaysShouhuLog),
                        IsConfiged = ftpClient.DirectoryExists(configPath),
                        IsRecordInMySql = mysqlRecords.Any(x => CompareDns(x.computer_name, machineDir.Name)),
                        LogsPath = logsPath,
                        MachineDir = machineDir.FullName,
                        Name = machineDir.Name,
                        MySqlConfig = mysqlConfig,
                    };

                    machineObjects.Add(machineObject);
                }
                index++;
            }

            foreach (var machineObject in machineObjects)
            {
                if (!machineObject.HasTodayJobWorkerLogsPath)
                {
                    if (machineObject.IsConfiged)
                    {
                        var otherObject = machineObjects.FirstOrDefault(x => x.FtpClient != machineObject.FtpClient && CompareDns(x.Name, machineObject.Name));
                        if (otherObject != null)
                        {
                            if (!machineObject.IsRecordInMySql && otherObject.IsRecordInMySql)
                            {
                                machineObject.Operation = MachineOperation.DeleteMachineDirectory;
                            }
                        }
                    }
                    else
                    {
                        machineObject.Operation = MachineOperation.NeedConfig;
                    }

                }
                if (machineObject.HasTodayJobWorkerLogsPath && !machineObject.IsRecordInMySql)
                {
                    var otherObject = machineObjects.FirstOrDefault(x => x.FtpClient != machineObject.FtpClient && CompareDns(x.Name, machineObject.Name));
                    if (otherObject != null && otherObject.IsRecordInMySql)
                    {
                        machineObject.Operation = MachineOperation.MigrateToOtherServer;
                    }

                }
            }

            foreach (var machineObject in machineObjects)
            {
                if (machineObject.Operation== MachineOperation.None)
                {
                    continue;
                }
                Console.WriteLine($"{machineObject.FtpClient.Host} {machineObject.Name} Operation:{machineObject.Operation}");
            }
        }
        

        private static  void GenMachineInfoStatus(Options options,
            MySqlConfig mySqlConfig, 
            string? csvFilePathString,
            string? csvOutFilePathString,
            string channel)
        {
            try
            {
                var csvPath = Path.Combine(AppContext.BaseDirectory, "config", "daemonservicestatus.csv");
                if (!File.Exists(csvPath))
                {
                    return;
                }
                using var ftpClient = new FtpClient(options.host, options.username, options.password, options.port);

                var items = ftpClient.GetListing("/JobsWorkerDaemonService");
                HashSet<string> machineList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    if (item.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    machineList.Add(item.Name);
                }

                Dictionary<string, string> mapping = new Dictionary<string, string>()
                {
                    //厂区,测试业务,实验室区域,电脑所属实验室,电脑名称,登录账号,状态,计划完成时间,实际完成时间,备注
                    {"厂区","factory_name" },
                    {"测试业务","test_info" },
                    {"实验室区域","lab_area" },
                    {"电脑所属实验室","lab_name" },
                    {"电脑名称","computer_name" },
                    {"登录账号","login_name" },
                    {"状态","install_status" },
                    {"计划完成时间","plan_time" },
                    {"实际完成时间","actual_time" },
                    {"备注","remarks" }
                };
                List<MachineInfo> nodeInfoList = new List<MachineInfo>();
                using (var streamReader = new StreamReader(File.OpenRead(csvFilePathString)))
                {
                    Dictionary<string, int> headerDict = new Dictionary<string, int>();
                    while (!streamReader.EndOfStream)
                    {
                        var line = streamReader.ReadLine();
                        if (headerDict.Count == 0)
                        {
                            int index = 0;
                            foreach (var item in line.Split(',', StringSplitOptions.None))
                            {
                                if (!mapping.TryGetValue(item, out var columnName))
                                {
                                    continue;
                                }
                                headerDict.Add(mapping[item], index);
                                index++;
                            }
                        }
                        else
                        {
                            var segments = line.Split(',');
                            MachineInfo machineinfo = new MachineInfo();
                            machineinfo.factory_name = segments[headerDict[nameof(machineinfo.factory_name)]];
                            machineinfo.test_info = segments[headerDict[nameof(machineinfo.test_info)]];
                            machineinfo.lab_area = segments[headerDict[nameof(machineinfo.lab_area)]];
                            machineinfo.lab_name = segments[headerDict[nameof(machineinfo.lab_name)]];
                            machineinfo.computer_name = segments[headerDict[nameof(machineinfo.computer_name)]];
                            machineinfo.login_name = segments[headerDict[nameof(machineinfo.login_name)]];
                            machineinfo.host_name = "";
                            machineinfo.install_status = machineList.Contains(machineinfo.computer_name);
                            nodeInfoList.Add(machineinfo);
                        }


                    }
                }
                var factoryNameDict = new Dictionary<string, string>()
                {
                    {"bl","博罗" },
                    {"gm","光明" },
                };
                var result = SqlHelper.GetMachineInfoList(mySqlConfig);
                var mergedList = new List<MachineInfo>();
                foreach (var item in result)
                {
                    var nodeInfo = nodeInfoList.FirstOrDefault(
                        x => string.Equals(x.computer_name,
                        item.computer_name,
                        StringComparison.OrdinalIgnoreCase));
                    if (nodeInfo != null)
                    {
                        nodeInfo.install_status = item.install_status;
                    }
                    else
                    {
                        nodeInfo = new MachineInfo();
                        nodeInfo.computer_name = item.computer_name;
                        nodeInfo.factory_name = factoryNameDict[channel];
                        nodeInfo.install_status = true;
                        nodeInfo.test_info = string.Empty;
                        nodeInfo.lab_area = string.Empty;
                        nodeInfo.lab_name = string.Empty;
                        nodeInfo.login_name = string.Empty;
                        nodeInfo.host_name = string.Empty;
                        nodeInfo.update_time = string.Empty;
                        nodeInfo.version = string.Empty;
                    }
                    mergedList.Add(nodeInfo);
                }

                using (var fileStream = File.OpenWrite(csvOutFilePathString))
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine(string.Join(',', mapping.Keys));
                    foreach (var item in mergedList)
                    {
                        
                        streamWriter.WriteLine($"{item.factory_name},{item.test_info},{item.lab_area},{item.lab_name},{item.computer_name},{item.login_name},{item.install_status}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }


        private static void GenMarccorStatus(Options options, MySqlConfig mySqlConfig, string? csvFilePathString, string? csvOutFilePathString)
        {
            try
            {
                var csvPath = Path.Combine(AppContext.BaseDirectory, "config", "daemonservicestatus.csv");
                if (!File.Exists(csvPath))
                {
                    return;
                }
                using var ftpClient = new FtpClient(options.host, options.username, options.password, options.port);

                var items = ftpClient.GetListing("/JobsWorkerDaemonService");
                HashSet<string> machineList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    if (item.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    machineList.Add(item.Name);
                }

                Dictionary<string, string> mapping = new Dictionary<string, string>()
                {
                    //厂区,测试业务,实验室区域,电脑所属实验室,电脑名称,登录账号,状态,计划完成时间,实际完成时间,备注
                    {"厂区","factory_name" },
                    {"测试业务","test_info" },
                    {"实验室区域","lab_area" },
                    {"电脑所属实验室","lab_name" },
                    {"电脑名称","computer_name" },
                    {"登录账号","login_name" },
                    {"状态","install_status" },
                    {"计划完成时间","plan_time" },
                    {"实际完成时间","actual_time" },
                    {"备注","remarks" }
                };
                List<MachineInfo> nodeInfoList = new List<MachineInfo>();
                using (var streamReader = new StreamReader(File.OpenRead(csvFilePathString)))
                {
                    Dictionary<string, int> headerDict = new Dictionary<string, int>();
                    while (!streamReader.EndOfStream)
                    {
                        var line = streamReader.ReadLine();
                        if (headerDict.Count == 0)
                        {
                            int index = 0;
                            foreach (var item in line.Split(',', StringSplitOptions.None))
                            {
                                if (!mapping.TryGetValue(item, out var columnName))
                                {
                                    continue;
                                }
                                headerDict.Add(mapping[item], index);
                                index++;
                            }
                        }
                        else
                        {
                            var segments = line.Split(',');
                            MachineInfo machineinfo = new MachineInfo();
                            machineinfo.factory_name = segments[headerDict[nameof(machineinfo.factory_name)]];
                            machineinfo.test_info = segments[headerDict[nameof(machineinfo.test_info)]];
                            machineinfo.lab_area = segments[headerDict[nameof(machineinfo.lab_area)]];
                            machineinfo.lab_name = segments[headerDict[nameof(machineinfo.lab_name)]];
                            machineinfo.computer_name = segments[headerDict[nameof(machineinfo.computer_name)]];
                            machineinfo.login_name = segments[headerDict[nameof(machineinfo.login_name)]];
                            machineinfo.host_name = "";
                            machineinfo.install_status = machineList.Contains(machineinfo.computer_name);
                            nodeInfoList.Add(machineinfo);
                        }


                    }
                }

                var result = SqlHelper.GetMachineInfoList(mySqlConfig);

                foreach (var item in result)
                {
                    var oldItem = nodeInfoList.FirstOrDefault(x => x.computer_name == item.computer_name);
                    if (oldItem != null)
                    {
                        oldItem.install_status = item.install_status;
                    }
                }

                using (var fileStream = File.OpenWrite(csvOutFilePathString))
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine(string.Join(',', mapping.Keys));
                    foreach (var item in nodeInfoList)
                    {

                        streamWriter.WriteLine($"{item.factory_name},{item.test_info},{item.lab_area},{item.lab_name},{item.computer_name},{item.login_name},{item.install_status}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }

        private static void UpdateMachineInfo(Options options, MySqlConfig mySqlConfig, string csvFilePathString)
        {
            try
            {
                var csvPath = Path.Combine(AppContext.BaseDirectory, "config", "daemonservicestatus.csv");
                if (!File.Exists(csvPath))
                {
                    return;
                }
                using var ftpClient = new FtpClient(options.host, options.username, options.password, options.port);

                var items = ftpClient.GetListing("/JobsWorkerDaemonService");
                HashSet<string> machineList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    if (item.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    machineList.Add(item.Name);
                }

                Dictionary<string, string> mapping = new Dictionary<string, string>()
                {
                    //厂区,测试业务,实验室区域,电脑所属实验室,电脑名称,登录账号,状态,计划完成时间,实际完成时间,备注
                    {"厂区","factory_name" },
                    {"测试业务","test_info" },
                    {"实验室区域","lab_area" },
                    {"电脑所属实验室","lab_name" },
                    {"电脑名称","computer_name" },
                    {"登录账号","login_name" }
                };
                List<MachineInfo> nodeInfoList = new List<MachineInfo>();
                using (var streamReader = new StreamReader(File.OpenRead(csvFilePathString)))
                {
                    Dictionary<string, int> headerDict = new Dictionary<string, int>();
                    while (!streamReader.EndOfStream)
                    {
                        var line = streamReader.ReadLine();
                        if (headerDict.Count == 0)
                        {
                            int index = 0;
                            foreach (var item in line.Split(',', StringSplitOptions.None))
                            {
                                if (!mapping.TryGetValue(item, out var columnName))
                                {
                                    continue;
                                }
                                headerDict.Add(mapping[item], index);
                                index++;
                            }
                        }
                        else
                        {
                            var segments = line.Split(',');
                            MachineInfo machineinfo = new MachineInfo();
                            machineinfo.factory_name = segments[headerDict[nameof(machineinfo.factory_name)]];
                            machineinfo.test_info = segments[headerDict[nameof(machineinfo.test_info)]];
                            machineinfo.lab_area = segments[headerDict[nameof(machineinfo.lab_area)]];
                            machineinfo.lab_name = segments[headerDict[nameof(machineinfo.lab_name)]];
                            machineinfo.computer_name = segments[headerDict[nameof(machineinfo.computer_name)]];
                            machineinfo.login_name = segments[headerDict[nameof(machineinfo.login_name)]];
                            machineinfo.host_name = "";
                            machineinfo.install_status = machineList.Contains(machineinfo.computer_name);
                            nodeInfoList.Add(machineinfo);
                        }


                    }
                }

                SqlHelper.InsertOrUpdate(mySqlConfig, nodeInfoList, Console.WriteLine).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }


        private static void DeleteLogsCache(Options options)
        {
            using (var ftpClient = new FtpClient(options.host, options.username, options.password, options.port))
            {
                ftpClient.AutoConnect();
                if (!ftpClient.DirectoryExists("JobsWorkerDaemonService"))
                {
                    Console.WriteLine("Could not found directory JobsWorkerDaemonService");
                    return;
                }
                foreach (var machineDir in ftpClient.GetListing("JobsWorkerDaemonService").OrderBy(x => x.GetHashCode()))
                {
                    if (machineDir.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    var path = Path.Combine(machineDir.FullName, "uploadedlogs.bat");
                    if (ftpClient.FileExists(path))
                    {
                        ftpClient.DeleteFile(path);
                        Console.WriteLine($"{machineDir.FullName} deleted");
                    }
                }
            }
        }

        private static void UpdateConfig(Options options,MySqlConfig mySqlConfig, UpdateConfigParameters updateConfigParameters)
        {

            (string? LocalPath, FtpServerConfig? Config) GetRemoteConfig(FtpClient ftpClient, string channel, string configRoot)
            {
                var configRootPath = configRoot;
                var rootConfigDir = Path.Combine(configRootPath, channel, "config");
                var remoteServerBatConfigPath = Path.Combine(rootConfigDir, "server.bat");
                if (!ftpClient.FileExists(remoteServerBatConfigPath))
                {
                    return (null, null);
                }
                var localConfigPath = Path.Combine(AppContext.BaseDirectory, channel, "config");

                var localServerBatConfigPath = Path.Combine(localConfigPath, "server.bat");

                if (ftpClient.DownloadFile(localServerBatConfigPath, remoteServerBatConfigPath) != FtpStatus.Success)
                {
                    return (null, null);
                }

                var ftpConfig = JsonSerializer.Deserialize<FtpServerConfig>(File.ReadAllText(localServerBatConfigPath));

                var localConfigVersionDir = Path.Combine(localConfigPath, ftpConfig.version);

                var remoteConfigVersionDir = Path.Combine(rootConfigDir, ftpConfig.version);

                if (!Directory.Exists(localConfigVersionDir))
                {
                    Directory.CreateDirectory(localConfigVersionDir);
                }

                foreach (var item in ftpConfig.configfiles)
                {
                    var localPath = Path.Combine(localConfigVersionDir, item);
                    var remotePath = Path.Combine(remoteConfigVersionDir, item);
                    ftpClient.DownloadFile(localPath, remotePath);
                }


                return (localConfigPath, ftpConfig);
            }

            try
            {
                var factoryNameDict = new Dictionary<string, string>()
                {
                    {"bl","博罗" },
                    {"gm","光明" },
                };

                var machineList = SqlHelper.GetMachineInfoList(mySqlConfig);

                using (var ftpClient = new FtpClient(options.host, options.username, options.password, options.port))
                {
                    ftpClient.AutoConnect();

                    var (localConfigPath, ftpConfig) = GetRemoteConfig(ftpClient, updateConfigParameters.channel, updateConfigParameters.configRoot);

                    var pluginConfigPath = Path.Combine(AppContext.BaseDirectory, updateConfigParameters.channel, "plugins");

                    var pluginsConfigRootDir = $"/{updateConfigParameters.configRoot}/{updateConfigParameters.channel}/plugins";
                    bool existPluginDir = false;
                    if (ftpClient.DirectoryExists(pluginsConfigRootDir))
                    {
                        ftpClient.DownloadDirectory(pluginConfigPath, pluginsConfigRootDir, FtpFolderSyncMode.Mirror, FtpLocalExists.Overwrite);
                        existPluginDir = true;
                    }


                    //foreach (var item in machineList)
                    //{
                    //    if (item.factory_name != "光明")
                    //    {
                    //        continue;
                    //    }
                    //    if (!item.install_status)
                    //    {
                    //        continue;
                    //    }
                    //    ftpClient.CreateDirectory(Path.Combine("JobsWorkerDaemonService", item.computer_name));
                    //}

                    foreach (var machineDir in ftpClient.GetListing("JobsWorkerDaemonService").OrderBy(x => x.GetHashCode()))
                    {
                        if (machineDir.Type != FtpObjectType.Directory)
                        {
                            continue;
                        }
                        if (updateConfigParameters.filterType == "include")
                        {
                            if (!updateConfigParameters.hostNameList.Any(x => x.Equals(machineDir.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                        }
                        else if (updateConfigParameters.filterType == "exclude")
                        {
                            if (updateConfigParameters.hostNameList.Any(x => x.Equals(machineDir.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                        }
                        if (updateConfigParameters.initOnly)
                        {
                            var configPath = Path.Combine(machineDir.FullName, "config");
                            var jobsWorkerTodayLogFilePath = Path.Combine(machineDir.FullName, "logs", $"JobsWorker-{DateTime.Now.ToString("yyyy-MM-dd")}.log");
                            if (ftpClient.DirectoryExists(configPath) && !ftpClient.FileExists(jobsWorkerTodayLogFilePath))
                            {
                                continue;
                            }
                        }

                        //var nodeInfo = machineList.FirstOrDefault(
                        //    x => string.Equals(x.computer_name, machineDir.Name, StringComparison.OrdinalIgnoreCase)
                        //    );
                        //if (nodeInfo == null)
                        //{
                        //    Console.WriteLine(machineDir.Name);
                        //    continue;
                        //}

                        //if (!factoryNameDict.TryGetValue(updateConfigParameters.channel, out var factoryName)

                        //    ||

                        //    factoryName != nodeInfo.factory_name
                        //    )
                        //{
                        //    continue;
                        //}

                        //var nodeInfo = machineList.FirstOrDefault(
                        //    x => CompareDns(x.computer_name, machineDir.Name)
                        //    &&
                        //    x.factory_name == factoryNameDict[updateConfigParameters.channel]
                        //    );

                        //if (nodeInfo == null)
                        //{
                        //    //ftpClient.DeleteDirectory(machineDir.FullName);
                        //    Console.WriteLine(machineDir.Name);
                        //    continue;
                        //}
                        //else
                        //{
                        //    //Console.WriteLine($"{nodeInfo.factory_name} {nodeInfo.computer_name}");
                        //}

                        if (updateConfigParameters.deleteonly)
                        {
                            continue;

                        }

                        var remoteConfigDir = Path.Combine(machineDir.FullName, "config");
                        var pluginsConfigDir = Path.Combine(machineDir.FullName, "plugins");

                        if (existPluginDir)
                        {
                            ftpClient.UploadDirectory(pluginConfigPath, pluginsConfigDir, FtpFolderSyncMode.Update, FtpRemoteExists.Overwrite);
                        }


                        var result = ftpClient.UploadDirectory(localConfigPath, remoteConfigDir, FtpFolderSyncMode.Mirror, FtpRemoteExists.Overwrite);

                        RemoveCurrentLogFiles(ftpClient, machineDir);

                        Console.WriteLine($"Upload {remoteConfigDir}");

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        private static void RemoveCurrentLogFiles(FtpClient ftpClient, FtpListItem? machineDir)
        {
            var logsRecords = Path.Combine(machineDir.FullName, "uploadedlogs.bat");
            if (ftpClient.FileExists(logsRecords))
            {
                List<string> records = new List<string>();
                using (var stream = new MemoryStream())
                {
                    ftpClient.DownloadStream(stream, logsRecords);
                    stream.Position = 0;
                    using (var streamReader = new StreamReader(stream))
                    {
                        while (!streamReader.EndOfStream)
                        {
                            var line = streamReader.ReadLine();
                            if (line.StartsWith($"./logs\\JobWorker-{DateTime.Now.ToString("yyyy-MM-dd")}"))
                            {
                                continue;

                            }
                            records.Add(line);
                        }
                    }
                }
                using (var stream = new MemoryStream())
                using (var streamWriter = new StreamWriter(stream))
                {
                    foreach (var item in records)
                    {
                        streamWriter.WriteLine(item);
                    }
                    stream.Position = 0;
                    ftpClient.UploadStream(stream, logsRecords, FtpRemoteExists.Overwrite, true);
                }

            }
            var logsPath = Path.Combine(machineDir.FullName, "logs");
            var logFileName = $"JobWorker-{DateTime.Now.ToString("yyyy-MM-dd")}";
            foreach (var item in ftpClient.GetListing(logsPath))
            {
                if (item.Type != FtpObjectType.File)
                {
                    continue;
                }

                if (item.Name.StartsWith(logFileName))
                {
                    ftpClient.DeleteFile(item.FullName);
                }
            }
        }

        public static void CheckConfigDir(Options options, MySqlConfig mySqlConfig,string channel)
        {
            using (var ftpClient = new FtpClient(options.host, options.username, options.password, options.port))
            {
                ftpClient.AutoConnect();
                if (!ftpClient.DirectoryExists("JobsWorkerDaemonService"))
                {
                    Console.WriteLine("Could not found directory JobsWorkerDaemonService");
                    return;
                }
                var machineList = SqlHelper.GetMachineInfoList(mySqlConfig);
                var factoryNameDict = new Dictionary<string, string>()
                {
                    {"bl","博罗" },
                    {"gm","光明" },
                };

                List<MachineInfo> nodeInfoList = new List<MachineInfo>();
                foreach (var machineDir in ftpClient.GetListing("JobsWorkerDaemonService").OrderBy(x => x.GetHashCode()))
                {
                    if (machineDir.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    var configPath = Path.Combine(machineDir.FullName, "config");
                    if (!ftpClient.DirectoryExists(configPath))
                    {
                        Console.WriteLine(machineDir.Name);
                    }
                    else
                    {

                        var nodeInfo = machineList.FirstOrDefault(x =>
                        string.Equals(x.computer_name,
                        machineDir.Name,
                        StringComparison.OrdinalIgnoreCase));
                        if (nodeInfo != null)
                        {
                            continue;
                        }
                        nodeInfo = new MachineInfo();
                        nodeInfo.computer_name = machineDir.Name;
                        nodeInfo.factory_name = factoryNameDict[channel];
                        nodeInfo.install_status = true;
                        nodeInfo.test_info = string.Empty;
                        nodeInfo.lab_area = string.Empty;
                        nodeInfo.lab_name = string.Empty;
                        nodeInfo.login_name = string.Empty;
                        nodeInfo.host_name = string.Empty;
                        nodeInfo.update_time = string.Empty;
                        nodeInfo.version = string.Empty;
                        nodeInfoList.Add(nodeInfo);
                    }
                }
                if (nodeInfoList.Count > 0)
                {
                    SqlHelper.InsertOrUpdate(mySqlConfig, nodeInfoList, Console.WriteLine).Wait();
                }
            }

            static void CheckLog(FtpClient ftpClient, string machineName, IEnumerable<FtpListItem> logFiles, DateTime dateTime, MachineInfo nodeInfo)
            {
                var checkLists = new List<string>();
                checkLists.Add($"JobWorker{dateTime.ToString("-yyyy-MM-dd")}");
                checkLists.Add($"ShuCaiShouHu{dateTime.ToString("-yyyy-MM-dd")}");
                foreach (var checkItem in checkLists)
                {
                    if (!logFiles.Any(x => x.Name.StartsWith(checkItem)))
                    {
                        Console.WriteLine($"{machineName,-20} {nodeInfo.factory_name,-15} {nodeInfo.lab_name,-15} {nodeInfo.lab_area,-15} :missing {checkItem}");
                    }
                }
            }
        }

        public static void CheckLogDate(Options options, MySqlConfig mySqlConfig)
        {
            using (var ftpClient = new FtpClient(options.host, options.username, options.password, options.port))
            {
                ftpClient.AutoConnect();
                if (!ftpClient.DirectoryExists("JobsWorkerDaemonService"))
                {
                    Console.WriteLine("Could not found directory JobsWorkerDaemonService");
                    return;
                }
                var machineList = SqlHelper.GetMachineInfoList(mySqlConfig);

                foreach (var listItem in ftpClient.GetListing("JobsWorkerDaemonService").OrderBy(x => x.GetHashCode()))
                {
                    if (listItem.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    MachineInfo nodeInfo = FirstOrDefault(machineList, listItem.Name);
                    var remoteLogsPath = Path.Combine(listItem.FullName, "logs");
                    var logFiles = ftpClient.GetListing(remoteLogsPath).Where(x => x.Type == FtpObjectType.File);

                    var endDate = DateTime.Now.Date.AddDays(0);
                    var beginDate = DateTime.Now.AddDays(-1);

                    for (; endDate > beginDate; endDate = endDate.AddDays(-1))
                    {
                        CheckLog(ftpClient, listItem.Name, logFiles, endDate, nodeInfo);
                    }

                }
            }

            static void CheckLog(FtpClient ftpClient, string machineName, IEnumerable<FtpListItem> logFiles, DateTime dateTime, MachineInfo nodeInfo)
            {
                var checkLists = new List<string>();
                checkLists.Add($"JobWorker{dateTime.ToString("-yyyy-MM-dd")}");
                checkLists.Add($"ShuCaiShouHu{dateTime.ToString("-yyyy-MM-dd")}");
                foreach (var checkItem in checkLists)
                {
                    if (!logFiles.Any(x => x.Name.StartsWith(checkItem)))
                    {
                        Console.WriteLine($"{machineName,-20} {nodeInfo.factory_name,-15} {nodeInfo.lab_name,-15} {nodeInfo.lab_area,-15} :missing {checkItem}");
                    }
                }
            }
        }

        private static MachineInfo FirstOrDefault(List<MachineInfo> machineList, string name)
        {
            var nodeInfo = machineList.FirstOrDefault(
                x =>
                string.Equals(x.computer_name,
                name,
                StringComparison.OrdinalIgnoreCase));
            if (nodeInfo == null)
            {
                nodeInfo = new MachineInfo();
            }

            return nodeInfo;
        }

        public static void CheckLogException(Options options, bool summary,string outputfile)
        {

            string[] filters = new string[]
            {
                "because it is being used by another process"
            };

            if (!string.IsNullOrEmpty(outputfile))
            {
                var textWriter = new StreamWriter(outputfile);
                Console.SetOut(textWriter);
            }

            using (var ftpClient = new FtpClient(options.host, options.username, options.password, options.port))
            {
                ftpClient.AutoConnect();
                if (!ftpClient.DirectoryExists("JobsWorkerDaemonService"))
                {
                    Console.WriteLine("Could not found directory JobsWorkerDaemonService");
                    return;
                }
                foreach (var machineDir in ftpClient.GetListing("JobsWorkerDaemonService").OrderBy(x => x.GetHashCode()))
                {
                    if (machineDir.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    var remoteLogsPath = Path.Combine(machineDir.FullName, "logs");
                    var logFiles = ftpClient.GetListing(remoteLogsPath).Where(x => x.Type == FtpObjectType.File);

                    var endDate = DateTime.Now.Date;
                    var beginDate = DateTime.Now.AddDays(-1);

                    for (; endDate > beginDate; endDate = endDate.AddDays(-1))
                    {
                        CheckLogException(ftpClient, machineDir.Name, logFiles, filters, endDate, summary);
                    }

                }
            }
            static void CheckLogException(FtpClient ftpClient, string name, IEnumerable<FtpListItem> logFiles, string[] filters, DateTime dateTime, bool viewSummary)
            {
                var checkLists = new List<string>();
                checkLists.Add($"JobWorker{dateTime.ToString("-yyyy-MM-dd")}");
                checkLists.Add($"ShuCaiShouHu{dateTime.ToString("-yyyy-MM-dd")}");

                foreach (var checkItem in checkLists)
                {
                    foreach (var logFile in logFiles.Where(x => x.Name.StartsWith(checkItem)))
                    {
                        Dictionary<string, int> statDict = new Dictionary<string, int>();
                        Console.WriteLine(logFile.FullName);
                        using (var stream = new MemoryStream())
                        {
                            if (!ftpClient.DownloadStream(stream, logFile.FullName))
                            {
                                Console.WriteLine($"Download fail:{logFile.FullName}");
                                continue;
                            }
                            stream.Position = 0;

                            string templateString = "2023-12-27 16:36:43.7152 ";

                            using (var streamReader = new StreamReader(stream))
                            {
                                while (!streamReader.EndOfStream)
                                {
                                    var line = streamReader.ReadLine();

                                    if (line.IndexOf("Exception") < 0)
                                    {
                                        continue;
                                    }
                                    if (filters == null)
                                    {
                                        continue;
                                    }
                                    if (filters.Any(x => line.IndexOf(x) != -1))
                                    {
                                        continue;
                                    }
                                    if (viewSummary)
                                    {
                                        var content = line.Substring(templateString.Length);
                                        if (!statDict.TryGetValue(content, out var count))
                                        {
                                            statDict.Add(content, 1);
                                        }
                                        else
                                        {
                                            statDict[content]++;
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine(line);
                                    }

                                }

                            }
                        }
                        if (viewSummary)
                        {
                            foreach (var item in statDict)
                            {
                                Console.WriteLine(item.Key);
                                Console.WriteLine(item.Value);
                            }
                        }

                    }
                }




            }
        }

        public static void CheckUpdate(Options options, MySqlConfig mySqlConfig, DateTime datetime, string keywords)
        {
            string[] filters = new string[]
            {
                keywords,
            };
            using (var ftpClient = new FtpClient(options.host, options.username, options.password, options.port))
            {
                ftpClient.AutoConnect();
                if (!ftpClient.DirectoryExists("JobsWorkerDaemonService"))
                {
                    Console.WriteLine("Could not found directory JobsWorkerDaemonService");
                    return;
                }
                int updateMachineCount = 0;
                int missingMachineCount = 0;
                Dictionary<string, DateTime> updateDict = new Dictionary<string, DateTime>();
                List<string> misingMachineList = new List<string>();
                foreach (var machineDir in ftpClient.GetListing("JobsWorkerDaemonService").OrderBy(x => x.GetHashCode()))
                {
                    if (machineDir.Type != FtpObjectType.Directory)
                    {
                        continue;
                    }
                    var remoteLogsPath = Path.Combine(machineDir.FullName, "logs");
                    var logFiles = ftpClient.GetListing(remoteLogsPath).Where(x => x.Type == FtpObjectType.File);

                    var endDate = DateTime.Now;
                    var beginDate = DateTime.Now.AddDays(-1);

                    int updatedCount = 0;
                    for (; endDate > beginDate; endDate = endDate.AddDays(-1))
                    {
                        if (CheckLogUpdate(ftpClient, machineDir.Name, logFiles, filters, endDate, out var lastLogUpDateTime))
                        {
                            updateDict[machineDir.Name] = lastLogUpDateTime;
                            updatedCount++;
                        }
                    }
                    if (updatedCount > 0)
                    {
                        updateMachineCount++;
                    }
                    if (updatedCount == 0)
                    {
                        missingMachineCount++;
                        misingMachineList.Add(machineDir.Name);
                    }

                }
                Console.WriteLine($"Updated:{updateMachineCount} missing:{missingMachineCount}");

                var machineList = SqlHelper.GetMachineInfoList(mySqlConfig);

                Console.WriteLine("Updated:");

                foreach (var item in updateDict)
                {
                    MachineInfo nodeInfo = FirstOrDefault(machineList, item.Key);
                    if (item.Value > datetime)
                    {
                        Console.WriteLine($"{item.Key,-30}  {nodeInfo.factory_name,-15} {nodeInfo.lab_name,-15} {nodeInfo.lab_area,-15}  {item.Value}");
                    }
                    else
                    {
                        misingMachineList.Add(item.Key);
                    }

                }

                Console.WriteLine("Not updated yet:");

                foreach (var machineName in misingMachineList)
                {
                    MachineInfo nodeInfo = FirstOrDefault(machineList, machineName);
                    Console.WriteLine($"{machineName} {nodeInfo.factory_name,-15} {nodeInfo.lab_name,-15} {nodeInfo.lab_area,-15} ");
                }
            }

            static bool CheckLogUpdate(FtpClient ftpClient, string name, IEnumerable<FtpListItem> logFiles, string[] filters, DateTime dateTime, out DateTime lastLogDateTime)
            {
                lastLogDateTime = default;
                bool updated = false;
                bool fileChecked = false;
                var checkLists = new List<string>();
                checkLists.Add($"JobWorker{dateTime.ToString("-yyyy-MM-dd")}");
                foreach (var checkItem in checkLists)
                {
                    foreach (var logFile in logFiles.Where(x => x.Name.StartsWith(checkItem)))
                    {
                        using (var stream = new MemoryStream())
                        {
                            if (!ftpClient.DownloadStream(stream, logFile.FullName))
                            {
                                Console.WriteLine($"Download fail:{logFile.FullName}");
                                continue;
                            }
                            stream.Position = 0;
                            using (var streamReader = new StreamReader(stream))
                            {
                                string templateString = "2023-12-27 13:29:07.1197";
                                fileChecked = true;
                                Console.WriteLine(logFile.FullName);
                                string lastUpdatedLine = null;
                                while (!streamReader.EndOfStream)
                                {
                                    var line = streamReader.ReadLine();
                                    if (filters == null)
                                    {
                                        continue;
                                    }
                                    if (filters.All(x => line.IndexOf(x) != -1))
                                    {
                                        lastUpdatedLine = line;
                                        updated = true;
                                        continue;
                                    }
                                }
                                if (!updated)
                                {
                                    Console.WriteLine(false);
                                }
                                else
                                {
                                    if (lastUpdatedLine != null)
                                    {
                                        lastLogDateTime = DateTime.Parse(lastUpdatedLine.Substring(0, templateString.Length));
                                    }

                                    Console.WriteLine(true);

                                }
                            }
                        }
                    }
                }
                return updated;
            }
        }

    }
}
