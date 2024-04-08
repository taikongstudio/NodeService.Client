using FluentFTP;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System.Net;

namespace NodeService.WindowsService.Services
{
    public class FtpUploadJob : Job, IProgress<FtpProgress>
    {

        public FtpUploadJob(ApiService apiService, ILogger<FtpUploadJob> logger) : base(apiService, logger)
        {

        }

        private void ProcessFtpProgress(FtpProgress progress)
        {

        }

        public override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            FtpUploadJobOptions ftpUploadJobOptions = new FtpUploadJobOptions();
            await ftpUploadJobOptions.InitAsync(this.JobScheduleConfig, ApiService);
            foreach (var ftpUploadConfig in ftpUploadJobOptions.FtpUploadConfigs)
            {
                FtpUploadConfigExecutor ftpTaskExecutor = new FtpUploadConfigExecutor(this, ftpUploadConfig, Logger);
                await ftpTaskExecutor.ExecuteAsync(stoppingToken);
            }
        }

        public void Report(FtpProgress value)
        {

        }
    }
}
