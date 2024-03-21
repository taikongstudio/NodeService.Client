using FluentFTP;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using System.Net;

namespace NodeService.WindowsService.Services
{
    public class FtpUploadJob : Job
    {
        private readonly MyFtpProgress _myFtpProgress;

        public FtpUploadJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {
            _myFtpProgress = new MyFtpProgress(ProcessFtpProgress);
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
                FtpUploadConfigExecutor ftpTaskExecutor = new FtpUploadConfigExecutor(_myFtpProgress, ftpUploadConfig, Logger);
                await ftpTaskExecutor.ExecuteAsync();
            }
        }
    }
}
