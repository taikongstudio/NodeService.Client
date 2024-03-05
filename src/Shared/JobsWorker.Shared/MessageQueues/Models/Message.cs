namespace JobsWorker.Shared.MessageQueues.Models
{
    public abstract class Message : IKeyedObject<string>
    {
        public string Key { get; set; }
    }

    public abstract class Message<TContent> : Message, IMessage<string, TContent> where TContent : class
    {
        public required TContent Content { get; set; }
        public required DateTime DateTime { get; set; }
    }
}
