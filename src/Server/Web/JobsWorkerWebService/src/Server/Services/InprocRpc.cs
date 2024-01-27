
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;

namespace JobsWorkerWebService.Server.Services
{
    public class InprocRpc<TKey, TRequest, TResponse> : IInprocRpc<TKey, TRequest, TResponse> where TKey : notnull, IEquatable<TKey>
        where TRequest : class, IKeyedObject
        where TResponse : class, IKeyedObject

    {
        public InprocRpc()
        {
            this._reqChannelDict = new ConcurrentDictionary<TKey, Channel<TRequest>>();
            this._rspChannelDict = new ConcurrentDictionary<TKey, Channel<TResponse>>();
        }

        private PeriodicTimer _periodicTimer;
        private ConcurrentDictionary<TKey, Channel<TRequest>> _reqChannelDict;
        private ConcurrentDictionary<TKey, Channel<TResponse>> _rspChannelDict;

        private Channel<TRequest> EnsureRequestChannel(TKey key)
        {
            if (!_reqChannelDict.TryGetValue(key, out Channel<TRequest>? reqChannel))
            {
                reqChannel = Channel.CreateUnbounded<TRequest>();
                _reqChannelDict.TryAdd(key, reqChannel);
            }
            return reqChannel;
        }

        private Channel<TResponse> EnsureResponseChannel(TKey key)
        {
            if (!_rspChannelDict.TryGetValue(key, out Channel<TResponse>? rspChannel))
            {
                rspChannel = Channel.CreateUnbounded<TResponse>();
                _rspChannelDict.TryAdd(key, rspChannel);
            }
            return rspChannel;
        }

        public async Task<TResponseOveride?> SendRequestAsync<TResponseOveride>(TKey key, TRequest request, CancellationToken cancellationToken = default)
            where TResponseOveride : class, TResponse
        {
            TResponseOveride? response = default;
            var reqChannel = EnsureRequestChannel(key);
            var rspChannel = EnsureResponseChannel(key);
            await reqChannel.Writer.WriteAsync(request);
            do
            {
                if (await rspChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    response = await rspChannel.Reader.ReadAsync(cancellationToken) as TResponseOveride;
                    if (response.Id != request.Id)
                    {
                        await rspChannel.Writer.WriteAsync(response, cancellationToken);
                    }
                    break;
                }
            } while (!cancellationToken.IsCancellationRequested);
            return response;
        }

        public bool TryPeekRequest(TKey key, out TRequest? request)
        {
            var reqChannel = EnsureRequestChannel(key);
            return reqChannel.Reader.TryPeek(out request);
        }

        public bool TryReadRequest(TKey key, out TRequest? request)
        {
            var reqChannel = EnsureRequestChannel(key);
            return reqChannel.Reader.TryRead(out request);
        }

        public bool TryWriteResponse(TKey key, TResponse response)
        {
            var rspChannel = EnsureResponseChannel(key);
            return rspChannel.Writer.TryWrite(response);
        }
    }
}
