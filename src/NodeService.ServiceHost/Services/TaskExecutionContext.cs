using Google.Protobuf.WellKnownTypes;
using NodeService.Infrastructure.Concurrent;

namespace NodeService.ServiceHost.Services
{
    public class TaskExecutionContext : IAsyncDisposable
    {
        private readonly IAsyncQueue<JobExecutionReport> _reportChannel;
        private readonly ActionBlock<LogEntry> _logActionBlock;
        private readonly TaskExecutionContextDictionary _taskExecutionContextDictionary;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly BatchQueue<LogEntry> _logMessageEntryBatchQueue;
        private readonly ILogger _logger;

        public TaskExecutionContext(
            ILogger<TaskExecutionContext> logger,
            TaskCreationParameters parameters,
            IAsyncQueue<JobExecutionReport> reportQueue,
            TaskExecutionContextDictionary taskExecutionContextDictionary)
        {
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
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
                await foreach (var arrayPoolCollection in _logMessageEntryBatchQueue.ReceiveAllAsync(CancellationToken))
                {
                    foreach (var logStatusGroup in arrayPoolCollection.Where(static x => x.Status != (int)JobExecutionStatus.Unknown)
                                                                      .GroupBy(static x => x.Status))
                    {
                        var status = logStatusGroup.Key;
                        int logEntryCount = logStatusGroup.Count();
                        int pageSize = 512;
                        int pageCount = Math.DivRem(logEntryCount, pageSize, out int result);
                        if (result > 0)
                        {
                            pageCount = pageCount + 1;
                        }
                        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                        {
                            var entries = logStatusGroup.Skip(pageIndex * pageSize).Take(pageSize).Select(LogEntryToTaskExecutionLogEntry);
                            await EnqueueLogsAsync((JobExecutionStatus)status, entries);
                        }

                    }
                    arrayPoolCollection.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private static JobExecutionLogEntry LogEntryToTaskExecutionLogEntry(LogEntry logEntry)
        {
            return new JobExecutionLogEntry()
            {
                Type = (JobExecutionLogEntry.Types.JobExecutionLogEntryType)logEntry.Type,
                DateTime = Timestamp.FromDateTime(logEntry.DateTimeUtc),
                Value = logEntry.Value,
            };
        }

        public CancellationToken CancellationToken
        {
            get
            {
                return _cancellationTokenSource.Token;
            }
        }

        public ITargetBlock<LogEntry> LogMessageTargetBlock => _logActionBlock;


        private async Task WriteLogAsync(LogEntry logEntry)
        {
            logEntry.Status = (int)Status;
            await _logMessageEntryBatchQueue.SendAsync(logEntry);
        }



        public TaskCreationParameters Parameters { get; private set; }

        public JobExecutionStatus Status { get; private set; }

        public string Message { get; set; } = string.Empty;

        public bool CancelledManually { get; set; }


        public async Task UpdateStatusAsync(JobExecutionStatus status, string message)
        {
            Status = status;
            var report = new JobExecutionReport
            {
                Status = Status,
                Id = Parameters.Id,
                Message = message
            };
            report.Properties.Add(nameof(JobExecutionReport.CreatedDateTime), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            await _reportChannel.EnqueueAsync(report);
        }

        public async Task EnqueueLogsAsync(JobExecutionStatus status, IEnumerable<JobExecutionLogEntry> logEntries)
        {
            var report = new JobExecutionReport
            {
                Status = status,
                Id = Parameters.Id
            };
            report.LogEntries.AddRange(logEntries);
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
            _taskExecutionContextDictionary.TryRemove(Parameters.Id, out _);
            await Task.Delay(TimeSpan.FromSeconds(10));
            await _logMessageEntryBatchQueue.DisposeAsync();
        }
    }
}
