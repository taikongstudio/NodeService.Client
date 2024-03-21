﻿using System.Net;

namespace NodeService.WindowsService.Services.Helpers
{
    public static class CommonHelper
    {
        public static bool TryGetHostAddressStrings(ILogger logger, out string[]? hostAddressStrings)
        {
            hostAddressStrings = null;
            try
            {
                var hostAddresses = Dns.GetHostAddresses(Dns.GetHostName());
                hostAddressStrings = hostAddresses.Select(x => x.ToString()).ToArray();
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.ToString());
            }
            return false;
        }

        public static string GetIPAddressesString(ILogger logger)
        {
            string ipAddresesString = string.Empty;
            if (TryGetHostAddressStrings(logger, out var hostAddressStrings))
            {
                ipAddresesString = string.Join(",", hostAddressStrings);
            }

            return ipAddresesString;
        }

        public static List<ProcessInfo> CollectProcessList(ILogger logger)
        {
            List<ProcessInfo> processStatList = new List<ProcessInfo>();
            try
            {
                var processes = Process.GetProcesses();
                Dictionary<string, int> errors = new Dictionary<string, int>();
                foreach (var process in processes)
                {
                    try
                    {
                        ProcessInfo processStatInfo = new ProcessInfo()
                        {
                            FileName = process.ProcessName,
                            ProcessName = process.MainModule.FileName,
                            Id = process.Id,
                            Responding = process.Responding,
                            StartTime = process.StartTime.ToString("yyyy-MM-dd hh:mm:ss"),
                            ExitTime = process.HasExited ? process.ExitTime.ToString("yyyy-MM-dd hh:mm:ss") : null,
                            VirtualMemorySize64 = process.VirtualMemorySize64,
                            PagedMemorySize64 = process.PagedMemorySize64,
                            NonpagedSystemMemorySize64 = process.NonpagedSystemMemorySize64,
                            PagedSystemMemorySize64 = process.PagedSystemMemorySize64,
                            HandleCount = process.HandleCount,
                        };
                        processStatList.Add(processStatInfo);
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
                    logger?.LogError($"Count:{item.Value} Exception:{item.Key}");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.ToString());
            }
            return processStatList;
        }

    }
}