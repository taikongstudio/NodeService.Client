using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ftpservertool
{
    public class FtpServerConfig
    {
        public string server { get; set; }

        public string password { get; set; }

        public string username { get; set; }

        public string localLogDirectory { get; set; }



        public string remoteLogDirectoryFormat { get; set; }

        public string[] uploadLogJobCronExpressions { get; set; }

        public string version { get; set; }

        public string[] configfiles { get; set; }

        public PluginInfo[] plugins { get; set; }

        public int SleepSeconds { get; set; }

        public string AsJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public JobScheduleConfig AsJobScheduleConfig(string jobName)
        {
            return new JobScheduleConfig()
            {
                cronExpressions = uploadLogJobCronExpressions,
                isEnabled = true,
                jobName = jobName,
                arguments = new Dictionary<string, string> {
                    { nameof(server),server },
                    { nameof(password),password },
                    { nameof(username),username },
                    { nameof(localLogDirectory),localLogDirectory },
                    { nameof(remoteLogDirectoryFormat),remoteLogDirectoryFormat },
                },
            };
        }

        private static object s_lock = new object();

        public static LoadConfigResult TryLoadServerConfig(string configPath, ref int hashCode, Action<string> logger, out FtpServerConfig ftpServerConfig)
        {
            ftpServerConfig = null;
            try
            {
                if (!File.Exists(configPath))
                {
                    return LoadConfigResult.LoadFail;
                }
                string json = null;
                lock (s_lock)
                {
                    json = File.ReadAllText(configPath);
                }

                if (hashCode == json.GetHashCode())
                {
                    return LoadConfigResult.NotChanged;
                }
                ftpServerConfig = JsonSerializer.Deserialize<FtpServerConfig>(json);
                logger?.Invoke("Load config success:");
                hashCode = json.GetHashCode();
                return LoadConfigResult.Changed;
            }
            catch (Exception ex)
            {
                logger?.Invoke(ex.ToString());
                return LoadConfigResult.LoadFail;
            }

        }

        public static bool TryLoadServerConfig(string configPath, Action<string> logger, out FtpServerConfig ftpServerConfig)
        {
            ftpServerConfig = null;
            try
            {
                if (!File.Exists(configPath))
                {
                    return false;
                }
                string json = null;
                lock (s_lock)
                {
                    json = File.ReadAllText(configPath);
                }

                ftpServerConfig = JsonSerializer.Deserialize<FtpServerConfig>(json);
                logger?.Invoke("Load config success:");
                return true;
            }
            catch (Exception ex)
            {
                logger?.Invoke(ex.ToString());
                return false;
            }

        }


    }
}
