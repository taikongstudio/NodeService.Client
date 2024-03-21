using FluentFTP;
using NodeService.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class FtpDownloadJob : Job
    {
        private readonly MyFtpProgress _myFtpProgress;

        public FtpDownloadJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {
            _myFtpProgress = new MyFtpProgress(ProcessFtpProgress);
        }

        private void ProcessFtpProgress(FtpProgress progress)
        {

        }

        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            FtpDownloadJobOptions options = new FtpDownloadJobOptions();
            await options.InitAsync(this.JobScheduleConfig, ApiService);
            FtpDownloadConfigExecutor ftpDownloadConfigExecutor = new FtpDownloadConfigExecutor(_myFtpProgress, options.FtpDownloadConfig, this.Logger);
            await ftpDownloadConfigExecutor.ExecuteAsync(cancellationToken);

        }
    }
}
