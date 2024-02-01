using JobsWorkerNodeService.Interop.Util;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs
{
    internal class DetectProcessAndShowMessageBoxJob : JobBase
    {

        public DetectProcessAndShowMessageBoxJob()
        {

        }


        public async override Task Execute(IJobExecutionContext context)
        {
            try
            {
                var swj_software_regex = Arguments["swj_software_regex"];
                var check_window_warning_time = int.Parse(Arguments["check_window_warning_time"]);

                if (string.IsNullOrWhiteSpace(swj_software_regex) || swj_software_regex.Equals("null"))
                    return;
                var result = new List<string>();
                Regex r = new Regex(swj_software_regex.ToLower());
                var ps = Process.GetProcesses();
                foreach (var p in ps)
                {
                    if (r.IsMatch(p.ProcessName.ToLower()))
                    {
                        result.Add(p.ProcessName);
                    }
                }
                if (result.Count == 0)
                {
                    PerformanceHelper.ShowMessageBox($"请打开软件<{swj_software_regex}>,并点击启动按钮,否则测试数据无法上传到LIMS!", "发现异常",
                        check_window_warning_time / 1000);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }
    }
}
