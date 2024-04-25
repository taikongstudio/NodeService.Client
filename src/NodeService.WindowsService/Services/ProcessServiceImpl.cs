using Grpc.Core;
using NodeService.Infrastructure.Models;
using NodeService.WindowsService.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class ProcessServiceImpl : NodeService.Infrastructure.Services.ProcessService.ProcessServiceBase
    {
        private readonly ILogger<ProcessServiceImpl> _logger;
        private readonly ConcurrentDictionary<string, ProcessChannelInfo> _processChannelInfoDictionary;

        public ProcessServiceImpl(
            ILogger<ProcessServiceImpl> logger,
            [FromKeyedServices(Constants.ProcessChannelInfoDictionary)]
            ConcurrentDictionary<string, ProcessChannelInfo> processChannelInfoDictionary
            )
        {
            _logger = logger;
            _processChannelInfoDictionary = processChannelInfoDictionary;
        }

        public override async Task<Empty> KillAppProcesses(
            KillAppProcessRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation($"Parameters:{request}");

            foreach (var processInfoDictionary in _processChannelInfoDictionary.Values)
            {
                await processInfoDictionary.ProcessCommandChannel.Writer.WriteAsync(new ProcessCommandRequest()
                {
                    CommadType = ProcessCommandType.KillProcess,
                });
            }
            return new Empty();
        }

        public override Task<QueryAppProcessResponse> QueryAppProcesses(
            QueryAppProcessRequest request,
            ServerCallContext context)
        {
            QueryAppProcessResponse queryAppProcessResponse = new QueryAppProcessResponse()
            {
                RequestId = request.RequestId,
            };
            queryAppProcessResponse.AppProcesses.AddRange(_processChannelInfoDictionary.Values.Select(ConvertProcessToAppProcessInfo));
            return Task.FromResult(queryAppProcessResponse);
        }

        private AppProcessInfo ConvertProcessToAppProcessInfo(ProcessChannelInfo processChannelInfo)
        {
            AppProcessInfo appProcessInfo = new AppProcessInfo();
            try
            {
                appProcessInfo.HasExited = true;
                appProcessInfo.Id = processChannelInfo.Process.Id;
                appProcessInfo.Name = processChannelInfo.Process.ProcessName;
                appProcessInfo.HasExited = processChannelInfo.Process.HasExited;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return appProcessInfo;
        }

    }
}
