using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NodeService.ServiceHost.Helpers
{
    public class WindowsUtil
    {
        private ILogger _logger;

        public WindowsUtil(ILogger logger)
        {
            _logger = logger;
        }

        public void UpdateConfig(string swj_ips_regex, string swj_software_regex)
        {
            _swj_ips_regex = swj_ips_regex;
            _swj_software_regex = swj_software_regex;
            //check_window_warning_time = Convert.ToInt32(ConfigurationManager.AppSettings["check_window_warning_time"]);
        }


        private string _swj_ips_regex = null;

        private string _swj_software_regex = null;

        //private DateTime window_warning_times = DateTime.MinValue;
        //private int check_window_warning_time = 0;
        public List<string> GetIps()
        {
            List<string> result = new List<string>();
            Regex r = new Regex(_swj_ips_regex);
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {

                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (!string.IsNullOrWhiteSpace(_swj_ips_regex))
                        {
                            if (r.IsMatch(ip.ToString()))
                            {
                                result.Add(ip.ToString());
                            }
                        }
                        else
                            result.Add(ip.ToString());
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                return null;
            }
        }

        public List<string> GetProcess()
        {
            var result = new List<string>();
            try
            {
                if (!string.IsNullOrEmpty(_swj_software_regex))
                {
                    Regex r = new Regex(_swj_software_regex.ToLower());
                    var ps = Process.GetProcesses();
                    foreach (var p in ps)
                    {
                        if (r.IsMatch(p.ProcessName.ToLower()))
                        {
                            result.Add(p.ProcessName);
                        }
                    }
                    //if (result.Count == 0)
                    //{
                    //    if (!swj_software_regex.Equals("null"))
                    //    {
                    //        Console.WriteLine($"距离上次弹窗:{(DateTime.Now - window_warning_times).TotalSeconds}秒。");
                    //        if ((DateTime.Now - window_warning_times).TotalSeconds > check_window_warning_time)
                    //        {

                    //            window_warning_times = DateTime.Now;
                    //            Interop.ShowMessageBox($"请打开软件<{swj_software_regex}>,否则测试数据无法上传!", "发现异常");
                    //        }
                    //    }
                    //}
                }
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                return null;
            }
        }


        public string GetMACAddress()
        {
            string macAddress = "";
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    macAddress = nic.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(macAddress))
                        break;
                }
            }
            return macAddress;
        }

        public string GetNodeName()
        {
            return Dns.GetHostName();
        }

        public string GetUserName()
        {
            return Environment.UserName;
        }


    }

}
