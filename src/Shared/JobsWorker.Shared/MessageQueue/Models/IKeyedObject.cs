namespace JobsWorker.Shared.MessageQueue.Models
{
    public interface IKeyedObject<TKey>
    {
        public TKey Key { get; set; }
    }
}
