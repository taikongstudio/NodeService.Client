using System.Runtime.CompilerServices;
using JobsWorker.Shared.MessageQueue.Models;

namespace JobsWorker.Shared.MessageQueue
{
    public interface IInprocRpc<TTarget, TKey, TRequest, TResponse>
        where TTarget : notnull, IEquatable<TTarget>
        where TKey : notnull, IEquatable<TKey>
        where TRequest : class, IKeyedObject<TKey>
        where TResponse : class, IKeyedObject<TKey>
    {
        ValueTask<TResponseOveride?> SendAsync<TResponseOveride>(TTarget target,
            TRequest request,
            CancellationToken cancellationToken = default)
            where TResponseOveride : class, TResponse;

        ValueTask PostAsync(TTarget target, TRequest request, CancellationToken cancellationToken = default);

        ValueTask WriteResponseAsync(TTarget target, TResponse response, CancellationToken cancellationToken = default);

        IAsyncEnumerable<TRequestOveride> ReadAllRequestAsync<TRequestOveride>(TTarget target,
            Func<TRequestOveride, bool>? filter = null,
            CancellationToken cancellationToken = default
            )
            where TRequestOveride : class, TRequest;

        IAsyncEnumerable<TResponseOveride> ReadAllResponseAsync<TResponseOveride>(TTarget target,
            Func<TResponseOveride, bool>? filter = null,
            CancellationToken cancellationToken = default
            )
            where TResponseOveride : class, TResponse;
    }
}