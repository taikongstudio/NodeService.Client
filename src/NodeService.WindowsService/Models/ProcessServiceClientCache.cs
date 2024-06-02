﻿using Grpc.Net.Client;
using NodeService.ServiceHost.Grpc;
using static NodeService.Infrastructure.Services.ProcessService;

namespace NodeService.ServiceHost.Models
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
