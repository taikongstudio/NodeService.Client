using FluentFTP;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System.Net;

namespace NodeService.ServiceHost.Tasks
{
    public class LogUploadJob : TaskBase, IProgress<FtpProgress>
    {
        private readonly string _dnsName;

        public LogUploadJob(ApiService apiService, ILogger<TaskBase> logger) : base(apiService, logger)
        {
            _dnsName = Dns.GetHostName();
        }

        void ProcessFtpProgress(FtpProgress progress)
        {
            if (progress.Progress == 100)
            {
                Logger.LogInformation($"LocalPath:{progress.LocalPath}=>RemotePath:{progress.RemotePath} Progress:{progress.Progress} TransferSpeed:{progress.TransferSpeed}");
            }

        }

        private static async Task UploadRecords(AsyncFtpClient ftpClient, string remoteUploadRecordDir, HashSet<string> pathHashSet, int addedCount)
        {
            if (addedCount > 0)
            {
                using var memoryStream = new MemoryStream();
                using var streamWriter = new StreamWriter(memoryStream);
                foreach (var path in pathHashSet)
                {
                    streamWriter.WriteLine(path);
                }
                memoryStream.Position = 0;
                await ftpClient.UploadStream(memoryStream, remoteUploadRecordDir);
            }
        }




        private async Task ExecuteLogUploadConfigAsync(AsyncFtpClient ftpClient, LogUploadConfigModel? logUploadConfig)
        {

        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                LogUploadJobOptions logUploadOptions = new LogUploadJobOptions();
                await logUploadOptions.InitAsync(JobScheduleConfig, ApiService, stoppingToken);

                foreach (var uploadLogConfig in logUploadOptions.LogUploadConfigs)
                {
                    LogUploadConfigExecutor logUploadConfigExecutor = new LogUploadConfigExecutor(this, uploadLogConfig, Logger);
                    await logUploadConfigExecutor.ExecutionAsync(stoppingToken);
                }



            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        public void Report(FtpProgress value)
        {

        }

    }
}
