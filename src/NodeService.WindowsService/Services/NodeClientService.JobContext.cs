using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.MessageQueues;
using NodeService.Infrastructure.Models;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using System.Runtime.CompilerServices;
using NodeService.WindowsService.Plugins;
using NodeService.WindowsService.Services;
using System.Threading.Channels;

namespace NodeService.WindowsService.Services
{
    public partial class NodeClientService
    {
        public class JobContext : IDisposable
        {
            private readonly string _nodeName;
            private readonly IServiceProvider _serviceProvider;
            private readonly Channel<JobExecutionReport> _reportChannel;
            private readonly ActionBlock<LogMessageEntry> _logActionBlock;
            private CancellationTokenSource _cancellationTokenSource;

            private ILogger _logger;

            public JobContext(
                IServiceProvider serviceProvider,
                string nodeName,
                JobExecutionParameters jobExecutionParameters,
                Channel<JobExecutionReport> reportChannel)
            {
                _nodeName = nodeName;
                _cancellationTokenSource = new CancellationTokenSource();
                _serviceProvider = serviceProvider;
                _reportChannel = reportChannel;
                _logger = _serviceProvider.GetService<ILogger<JobContext>>();
                _logActionBlock = new ActionBlock<LogMessageEntry>(WriteLogAsync, new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true,
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

            public bool StopManually {  get; set; }



            public void Cancel()
            {
                StopManually = true;
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
                    CreatedDateTime = DateTime.Now,
                    NodeName = _nodeName,
                    Status = Status
                };
                report.LogEntries.Add(jobExecutionLogEntry);
                report.Properties.Add(nameof(JobExecutionInstanceModel.Id), this.Parameters.Id);
                report.Properties.Add(nameof(JobExecutionInstanceModel.FireInstanceId), this.Parameters.FireInstanceId);
                switch (status)
                {
                    case JobExecutionStatus.Pendding:
                    case JobExecutionStatus.Started:
                    case JobExecutionStatus.Running:
                        report.Properties.Add(nameof(JobExecutionInstanceModel.ExecutionBeginTime), DateTime.Now.ToString(NodePropertyModel.DateTimeFormatString));
                        break;
                    case JobExecutionStatus.Failed:
                    case JobExecutionStatus.Finished:
                    case JobExecutionStatus.Cancelled:
                        report.Properties.Add(nameof(JobExecutionInstanceModel.ExecutionEndTime), DateTime.Now.ToString(NodePropertyModel.DateTimeFormatString));
                        break;
                    default:
                        break;
                }
                await _reportChannel.Writer.WriteAsync(report);
            }

            public void Dispose()
            {

            }
        }
    }
}
