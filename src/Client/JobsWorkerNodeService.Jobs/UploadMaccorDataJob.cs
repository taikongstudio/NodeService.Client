using JobsWorker.Shared.DataModels;
using JobsWorkerNodeService.Jobs.Models;
using MaccorDataUpload.Models;
using MaccorDataUpload.Workers;
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


        public override async Task Execute(IJobExecutionContext context)
        {
            try
            {
                UploadMaccorDataJobOptions options = this.JobScheduleConfig.GetOptions<UploadMaccorDataJobOptions>();
                var path = options.path;
                var broker_list = options.broker_list;
                var header_topic_name = options.header_topic_name;
                var time_data_topic_name = options.time_data_topic_name;
                var maxDegreeOfParallelism = options.maxDegreeOfParallelism;
                var mySqlConfigName = options.mysqlConfigName;
                var dbName = options.dbName;


                if (string.IsNullOrEmpty(path))
                {
                    foreach (var driveInfo in DriveInfo.GetDrives())
                    {
                        path = Path.Combine(driveInfo.RootDirectory.FullName, "Maccor", "System");
                        this.Logger.LogInformation($"try use maccor path:{path}");
                        if (Directory.Exists(path))
                        {
                            break;
                        }
                    }
                }
                if (!Directory.Exists(path))
                {
                    this.Logger.LogError($"Could not found Maccor path:{path}");
                    return;
                }


                DirectoryInfo directoryInfo = directoryInfo = new DirectoryInfo(path);

                var mysqlConfig = this.NodeConfigTemplate.FindMysqlConfig(mySqlConfigName);

                if (mySqlConfigName == null)
                {
                    this.Logger.LogError($"Could not find mysql config:{mySqlConfigName}");
                    return;
                }

                foreach (var dir in directoryInfo.GetDirectories())
                {
                    var dataPath = Path.Combine(dir.FullName, "Archive");

                    DataReportServiceConfig serviceConfig = new DataReportServiceConfig()
                    {
                        path = dataPath,
                        broker_list = broker_list,
                        header_topic_name = header_topic_name,
                        time_data_topic_name = time_data_topic_name,
                        maxDegreeOfParallelism=  maxDegreeOfParallelism,
                        dbName= dbName,
                    };

                    if (!Directory.Exists(dataPath))
                    {
                        this.Logger.LogError($"Could not found directory:{dataPath}");
                        continue;
                    }
                    var key = $"DataFileCollectorWorker:{dataPath}";

                    SetupWorker(key, serviceConfig, mysqlConfig);
                }


            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }
        }

        private void SetupWorker(
            string key,
            DataReportServiceConfig serviceConfig,
            MysqlConfigModel mySqlConfig
            )
        {
            try
            {

                var dataFileCollectorWorker = AppDomain.CurrentDomain.GetData(key) as DataFileCollectorWorker;
                if (dataFileCollectorWorker == null)
                {
                    dataFileCollectorWorker = RunWorker(serviceConfig, mySqlConfig);
                    AppDomain.CurrentDomain.SetData(key, dataFileCollectorWorker);
                }
                else
                {
                    dataFileCollectorWorker.IsUpdatingConfig = true;

                    if (dataFileCollectorWorker.ServiceConfig != null
                        &&
                        dataFileCollectorWorker.ServiceConfig.AsJson() != serviceConfig.AsJson())
                    {
                        dataFileCollectorWorker.UpdateServiceConfig(serviceConfig);
                    }

                    if (dataFileCollectorWorker.MysqlConfig != null
                        &&
                        dataFileCollectorWorker.MysqlConfig.ToJsonString<MysqlConfigModel>() != mySqlConfig.ToJsonString<MysqlConfigModel>())
                    {
                        dataFileCollectorWorker.UpdateMySqlConfig(mySqlConfig);
                    }

                    if (dataFileCollectorWorker.DbName != serviceConfig.dbName)
                    {
                        dataFileCollectorWorker.InitializeSqliteDb(serviceConfig.dbName, true);
                    }

                    dataFileCollectorWorker.IsUpdatingConfig = false;
                }

            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.ToString());
            }

        }

        private DataFileCollectorWorker RunWorker(
    DataReportServiceConfig dataReportServiceConfig,
    MysqlConfigModel mySqlConfig)
        {

            DataFileCollectorWorker dataFileCollectorWorker = new DataFileCollectorWorker(this.Logger);
            dataFileCollectorWorker.UpdateServiceConfig(dataReportServiceConfig);
            dataFileCollectorWorker.UpdateMySqlConfig(mySqlConfig);
            dataFileCollectorWorker.InitializeSqliteDb(dataReportServiceConfig.dbName);
            dataFileCollectorWorker.Start();
            return dataFileCollectorWorker;
        }


    }

    public class ServiceConfig
    {
        public string Path { get; }
        public string Broker_list { get; }
        public string Header_topic_name { get; }
        public string Time_data_topic_name { get; }
        public int MaxDegreeOfParallelism { get; }
        public string DbName { get; }

        public ServiceConfig(string path, string broker_list, string header_topic_name, string time_data_topic_name, int maxDegreeOfParallelism, string dbName)
        {
            Path = path;
            Broker_list = broker_list;
            Header_topic_name = header_topic_name;
            Time_data_topic_name = time_data_topic_name;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
            DbName = dbName;
        }

        public override bool Equals(object? obj)
        {
            return obj is ServiceConfig other &&
                   Path == other.Path &&
                   Broker_list == other.Broker_list &&
                   Header_topic_name == other.Header_topic_name &&
                   Time_data_topic_name == other.Time_data_topic_name &&
                   MaxDegreeOfParallelism == other.MaxDegreeOfParallelism &&
                   DbName == other.DbName;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Path, Broker_list, Header_topic_name, Time_data_topic_name, MaxDegreeOfParallelism, DbName);
        }
    }
}
