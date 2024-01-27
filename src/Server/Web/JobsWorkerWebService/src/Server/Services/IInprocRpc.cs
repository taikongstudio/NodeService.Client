namespace JobsWorkerWebService.Server.Services
{
    public interface IInprocRpc<TKey, TRequest, TResponse>
        where TKey : notnull, IEquatable<TKey>
        where TRequest : class, IKeyedObject
        where TResponse : class, IKeyedObject
    {
        Task<TResponseOveride?> SendRequestAsync<TResponseOveride>(TKey key, TRequest request, CancellationToken cancellationToken = default) where TResponseOveride : class, TResponse;
        bool TryPeekRequest(TKey key, out TRequest? request);
        bool TryReadRequest(TKey key, out TRequest? request);
        bool TryWriteResponse(TKey key, TResponse response);
    }
}