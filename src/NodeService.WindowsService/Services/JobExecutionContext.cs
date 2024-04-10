﻿
using NodeService.Infrastructure.Interfaces;
using NodeService.Infrastructure.Logging;
using NodeService.WindowsService.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NodeService.WindowsService.Services
{
    public class JobExecutionContext : IAsyncDisposable
    {
        private readonly IAsyncQueue<JobExecutionReport> _reportChannel;
        private readonly ActionBlock<LogEntry> _logActionBlock;
        private readonly JobExecutionContextDictionary _jobExecutionContextDictionary;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly BatchQueue<LogEntry> _logMessageEntryBatchQueue;

        private ILogger _logger;

        public JobExecutionContext(
            ILogger<JobExecutionContext> logger,
            JobCreationParameters parameters,
            IAsyncQueue<JobExecutionReport> reportQueue,
            JobExecutionContextDictionary jobContextDictionary)
        {
            _jobExecutionContextDictionary = jobContextDictionary;
            _cancellationTokenSource = new CancellationTokenSource();
            _reportChannel = reportQueue;
            _logger = logger;
            _logActionBlock = new ActionBlock<LogEntry>(WriteLogAsync, new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true
            });
            Parameters = parameters;
            _logMessageEntryBatchQueue = new BatchQueue<LogEntry>(1024, TimeSpan.FromSeconds(3));
            _ = Task.Run(ProcessLogMessageEntries);
        }

        private async void ProcessLogMessageEntries()
        {
            try
            {
                await foreach (var arrayPoolCollection in this._logMessageEntryBatchQueue.ReceiveAllAsync(this.CancellationToken))
                {
                    foreach (var logStatusGroup in arrayPoolCollection.Where(static x => x.Status != (int)JobExecutionStatus.Unknown)
                                                                      .GroupBy(static x => x.Status))
                    {
                        var status = logStatusGroup.Key;
                        await this.EnqueueLogsAsync((JobExecutionStatus)status, logStatusGroup.Select(LogMessageEntryToJobExecutionLogEntry));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private static JobExecutionLogEntry LogMessageEntryToJobExecutionLogEntry(LogEntry logMessageEntry)
        {
            return new JobExecutionLogEntry()
            {
                Type = (JobExecutionLogEntry.Types.JobExecutionLogEntryType)logMessageEntry.Type,
                DateTime = Timestamp.FromDateTime(logMessageEntry.DateTimeUtc),
                Value = logMessageEntry.Value,
            };
        }

        public CancellationToken CancellationToken
        {
            get
            {
                return _cancellationTokenSource.Token;
            }
        }

        public ITargetBlock<LogEntry> LogMessageTargetBlock => this._logActionBlock;


        private async Task WriteLogAsync(LogEntry logEntry)
        {
            logEntry.Status = (int)this.Status;
            await this._logMessageEntryBatchQueue.SendAsync(logEntry);
        }



        public JobCreationParameters Parameters { get; private set; }

        public JobExecutionStatus Status { get; private set; }

        public string Message { get; set; } = string.Empty;

        public bool CancelledManually { get; set; }


        public async Task UpdateStatusAsync(JobExecutionStatus status, string message)
        {
            Status = status;
            var report = new JobExecutionReport()
            {
                Status = Status
            };
            report.Id = this.Parameters.Id;
            report.Message = message;
            report.Properties.Add(nameof(JobExecutionReport.CreatedDateTime), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            await _reportChannel.EnqueueAsync(report);
        }

        public async Task EnqueueLogsAsync(JobExecutionStatus status, IEnumerable<JobExecutionLogEntry> logMessageEntries)
        {
            var report = new JobExecutionReport()
            {
                Status = status
            };
            report.Id = this.Parameters.Id;
            report.LogEntries.AddRange(logMessageEntries);
            report.Properties.Add(nameof(JobExecutionReport.CreatedDateTime), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            await _reportChannel.EnqueueAsync(report);
        }

        public async ValueTask DisposeAsync()
        {
            CancelledManually = true;
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            _jobExecutionContextDictionary.TryRemove(Parameters.Id, out _);
            await Task.Delay(TimeSpan.FromSeconds(15));
            await this._logMessageEntryBatchQueue.DisposeAsync();
        }
    }
}
