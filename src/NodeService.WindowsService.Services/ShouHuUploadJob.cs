using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.WindowsService.Services.Helpers;
using System.Net;

namespace NodeService.WindowsService.Services
{
    public class ShouHuUploadJob : Job
    {
        private ProducerUtil _producerUtil;
        private WindowsUtil _windowsUtil;

        public ShouHuUploadJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {
        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                ShouHuUploadJobOptions options = new ShouHuUploadJobOptions();
               await options.InitAsync(this.JobScheduleConfig, ApiService);
                Logger.LogInformation("开始上报");

                _producerUtil = new ProducerUtil(Logger);
                _windowsUtil = new WindowsUtil(Logger);

                _producerUtil.UpdateConfig(options.KafkaConfig);
                Dictionary<string, object> uploadDic = new Dictionary<string, object>();
                string swj_software_regex = options.swj_software_regex;
                string swj_ips_regex = options.swj_ips_regex;
                _windowsUtil.UpdateConfig(swj_ips_regex, swj_software_regex);
                var swjFlag = options.swj_flag;
                if (swjFlag == string.Empty)
                {
                    swjFlag = Dns.GetHostName();
                }

                uploadDic.Add("swj_flag", swjFlag);
                uploadDic.Add("swj_vendor", options.swj_vendor);
                uploadDic.Add("swj_other_info", options.swj_other_info);
                uploadDic.Add("swj_ips", _windowsUtil.GetIps());
                uploadDic.Add("sw_mac", _windowsUtil.GetMACAddress());
                uploadDic.Add("sw_machine_name", _windowsUtil.GetNodeName());
                uploadDic.Add("sw_user_name", _windowsUtil.GetUserName());
                uploadDic.Add("swj_software", _windowsUtil.GetProcess());
                uploadDic.Add("windows_service_name", options.windows_service_name);
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
                PerformanceHelper.GetMemory(Logger, out a, out t);
                uploadDic.Add("mem_usage", $"{Math.Round(t, 0)} GB");
                //硬盘容量
                uploadDic.Add("desk_usage", PerformanceHelper.GetDeskInfo());
                //CPU信息
                uploadDic.Add("cpu_usage", PerformanceHelper.GetCPUInfo());
                string json = JsonSerializer.Serialize(uploadDic);
                Logger.LogInformation($"Msg:{json}");
                await _producerUtil.SendAsync(json);
                AppDomain.CurrentDomain.SetData("is_restart", "");
                Logger.LogInformation("上报成功");
            }
            catch (Exception ex)
            {
                Logger.LogError("上报失败");
                Logger.LogError(ex.ToString());
            }
            finally
            {

            }
        }
    }
}
