
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
    public class JobExecutionContext : IDisposable
    {
        private readonly IAsyncQueue<JobExecutionReport> _reportChannel;
        private readonly ActionBlock<LogMessageEntry> _logActionBlock;
        private readonly JobExecutionContextDictionary _jobContextDictionary;
        private CancellationTokenSource _cancellationTokenSource;

        private ILogger _logger;

        public JobExecutionContext(
            ILogger<JobExecutionContext> logger,
            JobExecutionParameters jobExecutionParameters,
            IAsyncQueue<JobExecutionReport> reportQueue,
            JobExecutionContextDictionary jobContextDictionary)
        {
            _jobContextDictionary = jobContextDictionary;
            _cancellationTokenSource = new CancellationTokenSource();
            _reportChannel = reportQueue;
            _logger = logger;
            _logActionBlock = new ActionBlock<LogMessageEntry>(WriteLogAsync, new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true
            });
            Parameters = jobExecutionParameters;
        }

        public CancellationToken CancellationToken
        {
            get
            {
                return _cancellationTokenSource.Token;
            }
        }

        public ITargetBlock<LogMessageEntry> LogMessageTargetBlock => this._logActionBlock;


        private async Task WriteLogAsync(LogMessageEntry log)
        {
            await this.UpdateStatusAsync(this.Status, new JobExecutionLogEntry()
            {
                DateTime = Timestamp.FromDateTime(log.DateTime.ToUniversalTime()),
                Type = (JobExecutionLogEntry.Types.JobExecutionLogEntryType)log.Type,
                Value = log.Value
            });
        }



        public JobExecutionParameters Parameters { get; private set; }

        public JobExecutionStatus Status { get; private set; }

        public string Message { get; set; } = string.Empty;

        public bool CancelledManually { get; set; }



        public void Cancel()
        {
            CancelledManually = true;
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }


        public async Task UpdateStatusAsync(JobExecutionStatus status, string message)
        {
            await UpdateStatusAsync(status, new JobExecutionLogEntry()
            {
                DateTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.Now.ToUniversalTime()),
                Type = (JobExecutionLogEntry.Types.JobExecutionLogEntryType)Microsoft.Extensions.Logging.LogLevel.Information,
                Value = message
            });
        }

        public async Task UpdateStatusAsync(JobExecutionStatus status, JobExecutionLogEntry jobExecutionLogEntry)
        {
            Status = status;
            var report = new JobExecutionReport()
            {
                Status = Status
            };
            report.LogEntries.Add(jobExecutionLogEntry);
            report.Properties.Add(nameof(JobExecutionInstanceModel.Id), this.Parameters.Id);
            report.Properties.Add(nameof(JobExecutionInstanceModel.FireInstanceId), this.Parameters.FireInstanceId);
            report.Properties.Add(nameof(JobExecutionReport.CreatedDateTime), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            await _reportChannel.EnqueueAsync(report);
        }

        public void Dispose()
        {
            _jobContextDictionary.TryRemove(Parameters.Id, out _);
            if (this._cancellationTokenSource != null)
            {
                this._cancellationTokenSource.Dispose();
            }
        }
    }
}
