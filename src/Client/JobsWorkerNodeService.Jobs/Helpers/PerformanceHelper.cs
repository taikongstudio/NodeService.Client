using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Helpers
{
    class PerformanceHelper
    {
        public static nint WTS_CURRENT_SERVER_HANDLE = nint.Zero;
        public static void ShowMessageBox(string message, string title, int timeout)
        {
            int resp = 0;
            WTSSendMessage(WTS_CURRENT_SERVER_HANDLE, WTSGetActiveConsoleSessionId(), title, title.Length, message, message.Length, 0, timeout, out resp, false);
        }
        [DllImport("kernel32.dll", SetLastError = true)] public static extern int WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool WTSSendMessage(nint hServer, int SessionId, string pTitle, int TitleLength, string pMessage, int MessageLength, int Style, int Timeout, out int pResponse, bool bWait);
        //获取内存状态
        [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] static extern bool GlobalMemoryStatusEx(MEMORYINFO mi);
        //Define the information structure of memory 

        [StructLayout(LayoutKind.Sequential)]
        public class MEMORYINFO
        {
            public uint dwLength; //Current structure size 
            public uint dwMemoryLoad; //Current memory utilization 
            public ulong ullTotalPhys; //Total physical memory size 
            public ulong ullAvailPhys; //Available physical memory size 
            public ulong ullTotalPageFile; //Total Exchange File Size 
            public ulong ullAvailPageFile; //Total Exchange File Size 
            public ulong ullTotalVirtual; //Total virtual memory size 
            public ulong ullAvailVirtual; //Available virtual memory size 
            public ulong ullAvailExtendedVirtual; //Keep this value always zero } 


        }

        /// <summary> 
        /// Get the current memory usage 
        /// </summary> 
        /// <returns></returns> 
        private static MEMORYINFO GetMemoryStatus()
        {
            MEMORYINFO memoryInfo = new MEMORYINFO();
            memoryInfo.dwLength = (uint)Marshal.SizeOf(memoryInfo);
            GlobalMemoryStatusEx(memoryInfo);
            return memoryInfo;
        }

        /// <summary> 
        /// 获取系统内存信息 
        /// </summary> 
        /// <param name="fileSizeUnit">默认单位GB</param> 
        /// <returns></returns> 
        public static bool GetMemory(ILogger logger,out double available, out double total)
        {
            available = 0;
            total = 0;
            try
            {
                var memoryStatus = GetMemoryStatus();
                available = ToFileFormat((long)memoryStatus.ullAvailPhys, FileSizeUnit.GB);
                total = ToFileFormat((long)memoryStatus.ullTotalPhys, FileSizeUnit.GB);
                return true;
            }
            catch (Exception e)
            {
                logger?.LogError(e.ToString());
                return false;
            }
        }


        /// <summary>
        /// 根据指定的文件大小单位，对输入的文件大小（字节表示）进行转换。
        /// </summary>
        /// <param name="filesize">文件文件大小，单位为字节。</param>
        /// <param name="targetUnit">目标单位。</param>
        /// <returns></returns>
        public static double ToFileFormat(long filesize, FileSizeUnit targetUnit = FileSizeUnit.MB)
        {
            double size = -1;
            switch (targetUnit)
            {
                case FileSizeUnit.KB: size = filesize / 1024.0; break;
                case FileSizeUnit.MB: size = filesize / 1024.0 / 1024; break;
                case FileSizeUnit.GB: size = filesize / 1024.0 / 1024 / 1024; break;
                case FileSizeUnit.TB: size = filesize / 1024.0 / 1024 / 1024 / 1024; break;
                case FileSizeUnit.PB: size = filesize / 1024.0 / 1024 / 1024 / 1024 / 1024; break;
                default: size = filesize; break;
            }
            return size;
        }

        /// <summary>
        /// 文件大小单位，包括从B至PB共六个单位。
        /// </summary>
        public enum FileSizeUnit
        {
            B,
            KB,
            MB,
            GB,
            TB,
            PB
        }
        public static double GetCpuInfo()
        {
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            float cpuUsage = cpuCounter.NextValue();
            //cpuUsage = cpuCounter.NextValue();
            Thread.Sleep(10);
            cpuUsage = cpuCounter.NextValue();
            Console.WriteLine("CPU 使用率: {0}%", cpuUsage);
            return cpuUsage;
        }

        public static Dictionary<string, string> GetDeskInfo()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    result.Add(drive.Name, $"{drive.TotalSize / 1024 / 1024 / 1024} GB");
                    //Console.WriteLine("{0} 硬盘使用情况：", drive.Name);
                    //Console.WriteLine("总容量：{0} GB", drive.TotalSize / 1024 / 1024 / 1024);
                    //Console.WriteLine("已使用容量：{0} GB", (drive.TotalSize - drive.AvailableFreeSpace) / 1024 / 1024 / 1024);
                    //Console.WriteLine("可用容量：{0} GB", drive.AvailableFreeSpace / 1024 / 1024 / 1024);
                }
            }
            return result;
        }
        /// <summary>
        /// 获取CPU利用率
        /// </summary>
        public static Dictionary<string, double> CpuUsed()
        {
            Dictionary<string, double> result = new Dictionary<string, double>();
            PerformanceCounter[] counters = new PerformanceCounter[Environment.ProcessorCount];
            for (int i = 0; i < counters.Length; i++)
            {
                counters[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
            }
            //循环2次是因为第一次可能取的全是0
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int i = 0; i < counters.Length; i++)
                {
                    double f = Math.Round(counters[i].NextValue() / 100, 2);
                    string key = $"CPU-{i}";
                    if (result.ContainsKey(key))
                    {
                        result[key] = f;
                    }
                    else
                        result.Add(key, f);
                }
                Thread.Sleep(500);
            }
            return result;
        }

        #region CPU信息

        /// <summary>
        /// CPU信息
        /// </summary>
        /// <returns></returns>
        public static CPUInfo GetCPUInfo()
        {
            var cpuInfo = new CPUInfo();
            var cpuInfoType = cpuInfo.GetType();
            var cpuInfoFields = cpuInfoType.GetProperties().ToList();
            var moc = new ManagementClass("Win32_Processor").GetInstances();
            foreach (var mo in moc)
            {
                foreach (var item in mo.Properties)
                {
                    if (cpuInfoFields.Exists(f => f.Name == item.Name))
                    {
                        var p = cpuInfoType.GetProperty(item.Name);
                        p.SetValue(cpuInfo, item.Value);
                    }
                }
            }
            return cpuInfo;
        }
    }

    /// <summary>
    /// CPU信息
    /// </summary>
    public class CPUInfo
    {
        /// <summary>
        /// 操作系统类型，32或64
        /// </summary>
        public uint AddressWidth { get; set; }
        /// <summary>
        /// 处理器的名称。如：Intel(R) Core(TM) i3-8100 CPU @ 3.60GHz
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 处理器的当前实例的数目。如：4。4核
        /// </summary>
        public uint NumberOfEnabledCore { get; set; }
        /// <summary>
        /// 用于处理器的当前实例逻辑处理器的数量。如：4。4线程
        /// </summary>
        public uint NumberOfLogicalProcessors { get; set; }
        /// <summary>
        /// 系统的名称。计算机名称。如：GREAMBWANG
        /// </summary>
        public string SystemName { get; set; }
    }

    #endregion

}
