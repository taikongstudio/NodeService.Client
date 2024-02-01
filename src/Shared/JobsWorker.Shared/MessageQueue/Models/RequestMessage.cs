namespace JobsWorker.Shared.MessageQueue.Models
{

    public abstract class RequestMessage : IKeyedObject<string>
    {
        public required string Key { get; set; }
    }

    public abstract class RequestMessage<TContent> : RequestMessage, IMessage<string, TContent> where TContent : class
    {
        public required TimeSpan Timeout { get; set; }
        public required TContent Content { get; set; }
        public required DateTime DateTime { get; set; }
    }

}
