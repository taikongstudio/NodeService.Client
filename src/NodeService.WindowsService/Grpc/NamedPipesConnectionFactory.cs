﻿using System.IO.Pipes;
using System.Security.Principal;

namespace NodeService.WindowsService.Grpc
{
    public class NamedPipesConnectionFactory
    {
        private readonly string pipeName;

        public NamedPipesConnectionFactory(string pipeName)
        {
            this.pipeName = pipeName;
        }

        public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _,
            CancellationToken cancellationToken = default)
        {
            var clientStream = new NamedPipeClientStream(
                serverName: ".",
                pipeName: pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.WriteThrough | PipeOptions.Asynchronous,
                impersonationLevel: TokenImpersonationLevel.Anonymous);

            try
            {
                await clientStream.ConnectAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                return clientStream;
            }
            catch
            {
                clientStream.Dispose();
                throw;
            }
        }
    }
}
