namespace JobsWorker.Shared.Models
{
    public class ApiResult
    {
        public int ErrorCode { get; set; }
        public string Message { get; set; }
    }

    public class ApiResult<T> : ApiResult
    {
        public T? Value { get; set; }
    }
}
