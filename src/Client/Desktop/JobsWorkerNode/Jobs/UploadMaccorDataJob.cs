using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySqlConfig = JobsWorkerNode.Models.MySqlConfig;

namespace JobsWorkerNode.Jobs
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
                var path = Arguments["path"];
                var broker_list = Arguments["broker_list"];
                var header_topic_name = Arguments["header_topic_name"];
                var time_data_topic_name = Arguments["time_data_topic_name"];
                var maxDegreeOfParallelism = int.Parse(Arguments["maxDegreeOfParallelism"]);

                var mysql_host = Arguments[nameof(MySqlConfig.mysql_host)];
                var mysql_database = Arguments[nameof(MySqlConfig.mysql_database)];
                var mysql_userid = Arguments[nameof(MySqlConfig.mysql_userid)];
                var mysql_password = Arguments[nameof(MySqlConfig.mysql_password)];

                var dbName = Arguments["dbName"];


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
                    mysql_host = mysql_host,
                    mysql_database = mysql_database,
                    mysql_userid = mysql_userid,
                    mysql_password = mysql_password,
                };

                foreach (var dir in directoryInfo.GetDirectories())
                {
                    var dataPath = Path.Combine(dir.FullName, "Archive");

                    object dataReportServiceConfig = new
                    {
                        path = dataPath,
                        broker_list = broker_list,
                        header_topic_name = header_topic_name,
                        time_data_topic_name = time_data_topic_name,
                        maxDegreeOfParallelism = maxDegreeOfParallelism,
                        dbName = dbName,
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

                var maccorProcessInfo = AppDomain.CurrentDomain.GetData(key) as MaccorUploadToolInstanceInfo;
                if (maccorProcessInfo == null)
                {
                    maccorProcessInfo = RunWorker(serviceConfig, mySqlConfig, dbName);
                    AppDomain.CurrentDomain.SetData(key, maccorProcessInfo);
                }
                else
                {
                    if (maccorProcessInfo.ServiceConfig != null
                        &&
                        maccorProcessInfo.ServiceConfig.AsJson() != serviceConfig.AsJson())
                    {
                        maccorProcessInfo.UpdateServiceConfig(serviceConfig);
                    }

                    if (maccorProcessInfo.MySqlConfig != null
                        &&
                        maccorProcessInfo.MySqlConfig.AsJson() != mySqlConfig.AsJson())
                    {
                        maccorProcessInfo.UpdateMySqlConfig(mySqlConfig);
                    }

                    if (maccorProcessInfo.DbName != serviceConfig.dbName)
                    {
                        maccorProcessInfo.InitializeDb(serviceConfig.dbName);
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

        private bool TryReadServerConfig()
        {

        }

        private bool TryReadMySqlConfig()
        {

        }



        private MaccorUploadToolInstanceInfo RunWorker(
            object dataReportServiceConfig,
            object mySqlConfig,
            string dbName)
        {

            DataFileCollectorWorker dataFileCollectorWorker = new DataFileCollectorWorker(Logger);
            dataFileCollectorWorker.UpdateServiceConfig(dataReportServiceConfig);
            dataFileCollectorWorker.UpdateMySqlConfig(mySqlConfig);
            dataFileCollectorWorker.InitializeDb(dbName);
            dataFileCollectorWorker.Start();
            return dataFileCollectorWorker;
        }

    }
}
