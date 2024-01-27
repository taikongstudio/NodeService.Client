using JobsWorkerNode.Interop.Kafka;
using JobsWorkerNode.Interop.Util;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorkerNode.Jobs
{
    public class ShouHuUploadJob : JobBase
    {
        private ProducerUtil _producerUtil;
        private WindowsUtil _windowsUtil;

        public ShouHuUploadJob()
        {
            if (AppDomain.CurrentDomain.GetData("is_restart") == null)
            {
                AppDomain.CurrentDomain.SetData("is_restart", "1");
            }
        }

        public async override Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(10, 60)));
            Logger.LogInformation("开始上报");
            try
            {
                _producerUtil = new ProducerUtil(Logger);
                _windowsUtil = new WindowsUtil(Logger);

                _producerUtil.UpdateConfig(Arguments["Kafka-BootstrapServers"]);
                Dictionary<string, object> uploadDic = new Dictionary<string, object>();
                string swj_software_regex = Arguments["swj_software_regex"];
                string swj_ips_regex = Arguments["swj_ips_regex"];
                _windowsUtil.UpdateConfig(swj_ips_regex, swj_software_regex);
                var swjFlag = Arguments["swj_flag"];
                if (swjFlag == string.Empty)
                {
                    swjFlag = Dns.GetHostName();
                }

                uploadDic.Add("swj_flag", swjFlag);
                uploadDic.Add("swj_vendor", Arguments["swj_vendor"]);
                uploadDic.Add("swj_other_info", Arguments["swj_other_info"]);
                uploadDic.Add("swj_ips", _windowsUtil.GetIps());
                uploadDic.Add("sw_mac", _windowsUtil.GetMACAddress());
                uploadDic.Add("sw_machine_name", _windowsUtil.GetMachineName());
                uploadDic.Add("sw_user_name", _windowsUtil.GetUserName());
                uploadDic.Add("swj_software", _windowsUtil.GetProcess());
                uploadDic.Add("windows_service_name", Arguments["windows_service_name"]);
                uploadDic.Add("send_datetime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                //是否开启了软件守护功能
                if (string.IsNullOrWhiteSpace(swj_software_regex) || swj_software_regex.Equals("null"))
                    uploadDic.Add("is_soft_shouhu", 0);
                else
                    uploadDic.Add("is_soft_shouhu", 1);
                //启动后只有第一次上报，上传该字段
                uploadDic.Add("is_restart", AppDomain.CurrentDomain.GetData("is_restart"));
                //内存容量
                double a, t;
                PerformanceHelper.GetMemory(out a, out t);
                uploadDic.Add("mem_usage", $"{Math.Round(t, 0)} GB");
                //硬盘容量
                uploadDic.Add("desk_usage", PerformanceHelper.GetDeskInfo());
                //CPU信息
                uploadDic.Add("cpu_usage", PerformanceHelper.GetCPUInfo());
                string json = JsonSerializer.Serialize(uploadDic);
                Logger.LogInformation($"Msg:{json}");
                _producerUtil.SendMsg(json);
                AppDomain.CurrentDomain.SetData("is_restart", "");
                Logger.LogInformation("上报成功");
            }
            catch (Exception ex)
            {
                Logger.LogError("上报失败");
                Logger.LogError(ex.ToString());
            }
        }

    }
}
