using FluentFTP;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System.Net;

namespace NodeService.WindowsService.Services
{
    public class LogUploadJob : Job
    {
        private readonly MyFtpProgress _myFtpProgress;
        private readonly string _dnsName;

        public LogUploadJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {
            _myFtpProgress = new MyFtpProgress(ProcessFtpProgress);
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
                await logUploadOptions.InitAsync(this.JobScheduleConfig, ApiService, stoppingToken);

                foreach (var uploadLogConfig in logUploadOptions.LogUploadConfigs)
                {
                    LogUploadConfigExecutor logUploadConfigExecutor = new LogUploadConfigExecutor(this.EventId, this._myFtpProgress, uploadLogConfig, this.Logger);
                    await logUploadConfigExecutor.ExecutionAsync(stoppingToken);
                }



            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
    }
}
