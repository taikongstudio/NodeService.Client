using FluentFTP;
using JobsWorker.Shared.MessageQueue;
using JobsWorkerNodeService.Models;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Services
{
    public class UploadFileToFtpServerService : BackgroundService
    {
        private readonly IInprocMessageQueue<string, string, UploadFileToFtpServerRequest> _inprocMessageQueue;
        private readonly IConfigurationStore _configurationStore;
        public  ILogger<UploadFileToFtpServerService> Logger { get; private set; }

        public UploadFileToFtpServerService(
            IInprocMessageQueue<string, string, UploadFileToFtpServerRequest> inprocMessageQueue,
            ILogger<UploadFileToFtpServerService> logger,
            IConfigurationStore configurationStore)
        {
            this._inprocMessageQueue = inprocMessageQueue;
            this.Logger = logger;
            this._configurationStore = configurationStore;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var uploadFileRequest in this._inprocMessageQueue
                          .ReadAllMessageAsync<UploadFileToFtpServerRequest>(
                        nameof(UploadFileToFtpServerService),
                        null,
                        stoppingToken))
                    {
                        this.Logger.LogInformation($"Processing:{JsonSerializer.Serialize(uploadFileRequest)}");

                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex.ToString());
                }

            }
        }

    }
}
