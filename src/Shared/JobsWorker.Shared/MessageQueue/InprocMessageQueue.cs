using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using JobsWorker.Shared.MessageQueue.Models;

namespace JobsWorker.Shared.MessageQueue
{
    public class InprocMessageQueue<TTarget, TKey, TMessage> : IInprocMessageQueue<TTarget, TKey, TMessage>
                where TTarget : notnull, IEquatable<TTarget>
                where TKey : notnull, IEquatable<TKey>
                where TMessage : class, IKeyedObject<TKey>
    {

        private readonly ConcurrentDictionary<TTarget, Channel<TMessage>> _channelDictionary;

        public InprocMessageQueue()
        {
            _channelDictionary = new ConcurrentDictionary<TTarget, Channel<TMessage>>();
        }

        private Channel<TMessage> EnsureTargetChannel(TTarget target)
        {
            if (!_channelDictionary.TryGetValue(target, out Channel<TMessage>? channel))
            {
                channel = Channel.CreateUnbounded<TMessage>();
                _channelDictionary.TryAdd(target, channel);
            }
            return channel;
        }

        public ValueTask PostMessageAsync(TTarget target, TMessage message, CancellationToken cancellationToken = default)
        {
            var channel = EnsureTargetChannel(target);
            return channel.Writer.WriteAsync(message, cancellationToken);
        }

        public async ValueTask PostMessageToAllTargets(TMessage message, CancellationToken cancellationToken = default)
        {
            foreach (var channel in _channelDictionary.Values)
            {
                await channel.Writer.WriteAsync(message, cancellationToken);
            }
        }

        public async IAsyncEnumerable<TMessageOveride> ReadAllMessageAsync<TMessageOveride>(TTarget target,
            Func<TMessageOveride, bool>? filter = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TMessageOveride : class, TMessage
        {
            var channel = EnsureTargetChannel(target);
            await foreach (TMessage msg in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (msg is TMessageOveride messageOveride)
                {
                    if (filter == null || filter != null && filter(messageOveride))
                    {
                        yield return messageOveride;
                        continue;
                    }
                }
                await channel.Writer.WriteAsync(msg, cancellationToken);
            }
            yield break;
        }
    }
}
