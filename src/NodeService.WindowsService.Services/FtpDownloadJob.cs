﻿using FluentFTP;
using NodeService.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class FtpDownloadJob : Job, IProgress<FtpProgress>
    {
        public FtpDownloadJob(ApiService apiService, ILogger<Job> logger) : base(apiService, logger)
        {

        }



        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            FtpDownloadJobOptions options = new FtpDownloadJobOptions();
            await options.InitAsync(this.JobScheduleConfig, ApiService, cancellationToken);
            FtpDownloadConfigExecutor ftpDownloadConfigExecutor = new FtpDownloadConfigExecutor(this, options.FtpDownloadConfig, this.Logger);
            await ftpDownloadConfigExecutor.ExecuteAsync(cancellationToken);

        }

        public void Report(FtpProgress value)
        {

        }
    }
}
