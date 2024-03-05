namespace JobsWorker.Shared.MessageQueues.Models
{
    public interface IKeyedObject<TKey>
    {
        public TKey Key { get; set; }
    }
}
