using JobsWorker.Shared.MessageQueues.Models;

namespace JobsWorker.Shared.MessageQueues
{
    public interface IInprocMessageQueue<TTarget, TKey, TMessage>
        where TTarget : notnull, IEquatable<TTarget>
        where TKey : notnull, IEquatable<TKey>
        where TMessage : class, IKeyedObject<TKey>
    {
        IAsyncEnumerable<TMessageOveride> ReadAllMessageAsync<TMessageOveride>(TTarget target,
            Func<TMessageOveride, bool>? filter = null,
            CancellationToken cancellationToken = default)
            where TMessageOveride : class, TMessage;

        ValueTask PostMessageAsync(TTarget target,
            TMessage message,
            CancellationToken cancellationToken = default);

        ValueTask PostMessageToAllTargets(TMessage message, CancellationToken cancellationToken = default);
    }
}
