using JobsWorker.Shared.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs
{
    internal class UploadMaccorDataJob : JobBase
    {

        class MaccorUploadToolInstanceInfo
        {
            public Process Process { get; set; }

            public string MySqlConfig { get; set; }

            public string ServiceConfig { get; set; }

            public string dbName { get; set; }
        }


        public override Task Execute(IJobExecutionContext context)
        {
            try
            {
                var path = options["path"];
                var broker_list = options["broker_list"];
                var header_topic_name = options["header_topic_name"];
                var time_data_topic_name = options["time_data_topic_name"];
                var maxDegreeOfParallelism = int.Parse(options["maxDegreeOfParallelism"]);

                var plugin_path = options["plugin_path"].Replace("$(WorkingDirectory)", AppContext.BaseDirectory);

                var mysql_host = options[nameof(MySqlConfig.mysql_host)];
                var mysql_database = options[nameof(MySqlConfig.mysql_database)];
                var mysql_userid = options[nameof(MySqlConfig.mysql_userid)];
                var mysql_password = options[nameof(MySqlConfig.mysql_password)];

                var dbName = options["dbName"];


                if (string.IsNullOrEmpty(path))
                {
                    path = "D:\\Maccor\\System";
                    Logger.LogError($"use default maccor path:{path}");
                }
                if (!Directory.Exists(path))
                {
                    var directoryTxtFile = "C:\\shouhu\\maccor.txt";
                    if (!File.Exists(directoryTxtFile))
                    {
                        Logger.LogError($"Could not found Maccor config file:{directoryTxtFile}");
                        return Task.CompletedTask;
                    }
                    path = File.ReadAllLines(directoryTxtFile).FirstOrDefault();
                }
                if (!Directory.Exists(path))
                {
                    Logger.LogError($"Could not found Maccor config file:{path}");
                    return Task.CompletedTask;
                }


                DirectoryInfo directoryInfo = directoryInfo = new DirectoryInfo(path);


                object mySqlConfig = new
                {
                    mysql_host,
                    mysql_database,
                    mysql_userid,
                    mysql_password,
                };

                foreach (var dir in directoryInfo.GetDirectories())
                {
                    var dataPath = Path.Combine(dir.FullName, "Archive");

                    object dataReportServiceConfig = new
                    {
                        path = dataPath,
                        broker_list,
                        header_topic_name,
                        time_data_topic_name,
                        maxDegreeOfParallelism,
                        dbName,
                    };

                    if (!Directory.Exists(dataPath))
                    {
                        Logger.LogError($"Could not found directory:{dataPath}");
                        continue;
                    }
                    var key = $"DataFileCollectorWorker:{dataPath}";

                    SetupWorker(dataPath, key, dataReportServiceConfig, mySqlConfig, dbName);
                }


            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            return Task.CompletedTask;
        }

        private void SetupWorker(
            string dataPath,
            string key,
            object serviceConfig,
            object mySqlConfig,
            string dbName)
        {
            try
            {


            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }


    }
}
