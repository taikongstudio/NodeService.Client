using Google.Protobuf.WellKnownTypes;
using NodeService.Infrastructure.Concurrent;
using System.Drawing.Printing;

namespace NodeService.ServiceHost.Services
{
    public class TaskExecutionContext : IAsyncDisposable
    {
        private readonly IAsyncQueue<TaskExecutionReport> _reportQueue;
        private readonly ActionBlock<LogEntry> _logActionBlock;
        private readonly TaskExecutionContextDictionary _taskExecutionContextDictionary;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly BatchQueue<LogEntry> _logMessageEntryBatchQueue;
        private readonly ILogger _logger;

        public CancellationToken CancellationToken
        {
            get
            {
                return _cancellationTokenSource.Token;
            }
        }

        public ITargetBlock<LogEntry> LogMessageTargetBlock => _logActionBlock;

        public TaskCreationParameters Parameters { get; private set; }

        public TaskExecutionStatus Status { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool CancelledManually { get; private set; }

        public TaskExecutionContext(
            ILogger<TaskExecutionContext> logger,
            TaskCreationParameters parameters,
            IAsyncQueue<TaskExecutionReport> reportQueue,
            TaskExecutionContextDictionary taskExecutionContextDictionary)
        {
            _taskExecutionContextDictionary = taskExecutionContextDictionary;
            _cancellationTokenSource = new CancellationTokenSource();
            _reportQueue = reportQueue;
            _logger = logger;
            _logActionBlock = new ActionBlock<LogEntry>(WriteLogAsync, new ExecutionDataflowBlockOptions()
            {
                EnsureOrdered = true
            });
            Parameters = parameters;
            _logMessageEntryBatchQueue = new BatchQueue<LogEntry>(TimeSpan.FromSeconds(3), 1024);
            _ = Task.Run(ProcessLogMessageEntries);
        }

        private async Task ProcessLogMessageEntries()
        {
            try
            {
                await foreach (var arrayPoolCollection in _logMessageEntryBatchQueue.ReceiveAllAsync(CancellationToken))
                {
                    foreach (var logStatusGroup in arrayPoolCollection.Where(static x => x.Status != (int)TaskExecutionStatus.Unknown)
                                                                      .GroupBy(static x => x.Status))
                    {
                        var status = logStatusGroup.Key;
                        foreach (var logEntries in logStatusGroup.Chunk(1024))
                        {
                            var entries = logEntries.Select(LogEntryToTaskExecutionLogEntry);
                            await EnqueueLogsAsync((TaskExecutionStatus)status, entries);
                        }
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                if (ex.CancellationToken != CancellationToken)
                {
                    _logger.LogError(ex.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private static TaskExecutionLogEntry LogEntryToTaskExecutionLogEntry(LogEntry logEntry)
        {
            return new TaskExecutionLogEntry()
            {
                Type = (TaskExecutionLogEntryType)logEntry.Type,
                DateTime = Timestamp.FromDateTime(logEntry.DateTimeUtc),
                Value = logEntry.Value,
            };
        }



        private async Task WriteLogAsync(LogEntry logEntry)
        {
            logEntry.Status = (int)Status;
            await _logMessageEntryBatchQueue.SendAsync(logEntry);
        }


        public async Task UpdateStatusAsync(
            TaskExecutionStatus status,
            string message,
            IEnumerable<TaskExecutionLogEntry>? logEntries = null,
            IEnumerable<KeyValuePair<string, string>>? props = null)
        {
            Status = status;
            var report = new TaskExecutionReport
            {
                Status = Status,
                Id = Parameters.Id,
                Message = message,
            };
            report.Properties.Add(nameof(TaskExecutionReport.CreatedDateTime), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            if (props != null)
            {
                foreach (var kv in props)
                {
                    report.Properties.TryAdd(kv.Key, kv.Value);
                }
            }
            if (logEntries != null)
            {
                report.LogEntries.AddRange(logEntries);
            }
            await _reportQueue.EnqueueAsync(report);
        }

        public async Task EnqueueLogsAsync(TaskExecutionStatus status, IEnumerable<TaskExecutionLogEntry> logEntries)
        {
            var report = new TaskExecutionReport
            {
                Status = status,
                Id = Parameters.Id
            };
            report.LogEntries.AddRange(logEntries);
            report.Properties.Add(nameof(TaskExecutionReport.CreatedDateTime), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            await _reportQueue.EnqueueAsync(report);
        }

        public ValueTask CancelAsync()
        {
            CancelledManually = true;
            return DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
            _taskExecutionContextDictionary.TryRemove(Parameters.Id, out _);
            await _logMessageEntryBatchQueue.DisposeAsync();
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            IsDisposed = true;
        }
    }
}
