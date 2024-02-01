namespace JobsWorker.Shared.MessageQueue.Models
{
    public abstract class ResponseMessage : IKeyedObject<string>
    {
        public required string Key { get; set; }
    }
    public abstract class ResponseMessage<TContent> : ResponseMessage, IMessage<string, TContent> where TContent : class
    {
        public required TContent Content { get; set; }
        public required DateTime DateTime { get; set; }
    }

}
