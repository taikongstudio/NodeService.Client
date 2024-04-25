using Grpc.Core;
using Grpc.Net.Client;
using NodeService.Infrastructure.Services;
using NodeService.WindowsService.Grpc;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static NodeService.Infrastructure.Services.ProcessService;

namespace NodeService.WindowsService.Models
{
    public class ProcessServiceClientCache : IDisposable
    {


        private GrpcChannel? _grpcChannel;

        public ProcessServiceClientCache(GrpcChannel grpcChannel)
        {
            _grpcChannel = grpcChannel;
            ProcessServiceClient = new ProcessServiceClient(_grpcChannel);
        }

        public ProcessServiceClient ProcessServiceClient { get; private set; }




        public static GrpcChannel CreateChannel(string pipeName, CancellationToken cancellationToken)
        {
            var connectionFactory = new NamedPipesConnectionFactory(pipeName);
            var socketsHttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, token) => await connectionFactory.ConnectAsync(context, cancellationToken),
                ConnectTimeout = TimeSpan.FromSeconds(60)
            };

            return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                HttpHandler = socketsHttpHandler
            });
        }

        public void Dispose()
        {
            if (this._grpcChannel != null)
            {
                this._grpcChannel.Dispose();
            }
        }
    }
}
