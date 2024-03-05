using JobsWorker.Shared.MessageQueues.Models;

namespace JobsWorker.Shared.MessageQueues
{
    public class InprocRpc<TTarget, TKey, TRequest, TResponse> :
        IInprocRpc<TTarget, TKey, TRequest, TResponse>
        where TTarget : notnull, IEquatable<TTarget>
        where TKey : notnull, IEquatable<TKey>
        where TRequest : class, IKeyedObject<TKey>
        where TResponse : class, IKeyedObject<TKey>
    {

        public InprocMessageQueue<TTarget, TKey, TRequest> _requestMessageQueue;
        public InprocMessageQueue<TTarget, TKey, TResponse> _responseMessageQueue;

        public InprocRpc()
        {
            _requestMessageQueue = new InprocMessageQueue<TTarget, TKey, TRequest>();
            _responseMessageQueue = new InprocMessageQueue<TTarget, TKey, TResponse>();
        }

        public ValueTask PostAsync(TTarget target, TRequest request, CancellationToken cancellationToken)
        {
            return _requestMessageQueue.PostMessageAsync(target, request, cancellationToken);
        }

        public IAsyncEnumerable<TRequestOveride> ReadAllRequestAsync<TRequestOveride>(TTarget target, Func<TRequestOveride, bool>? filter, CancellationToken cancellationToken)
            where TRequestOveride : class, TRequest
        {
            return _requestMessageQueue.ReadAllMessageAsync(target, filter, cancellationToken);
        }

        public IAsyncEnumerable<TResponseOveride> ReadAllResponseAsync<TResponseOveride>(TTarget target, Func<TResponseOveride, bool>? filter, CancellationToken cancellationToken)
                    where TResponseOveride : class, TResponse
        {
            return _responseMessageQueue.ReadAllMessageAsync(target, filter, cancellationToken);
        }

        public async ValueTask<TResponseOveride?> SendAsync<TResponseOveride>(TTarget target, TRequest request, CancellationToken cancellationToken)
            where TResponseOveride : class, TResponse
        {
            await _requestMessageQueue.PostMessageAsync(target, request, cancellationToken);
            await foreach (var item in _responseMessageQueue.ReadAllMessageAsync<TResponseOveride>(target, (response) => response.Key.Equals(request.Key), cancellationToken))
            {
                return item;
            }
            return default;
        }

        public ValueTask WriteResponseAsync(TTarget target, TResponse response, CancellationToken cancellationToken = default)
        {
            return _responseMessageQueue.PostMessageAsync(target, response, cancellationToken);
        }
    }
}
