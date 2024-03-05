namespace JobsWorker.Shared.MessageQueues.Models
{
    public interface IMessage<TKey, TContent> : IKeyedObject<TKey> where TContent : class
    {
        TKey Key { get; set; }

        TContent Content { get; set; }

        DateTime DateTime { get; set; }

    }
}
