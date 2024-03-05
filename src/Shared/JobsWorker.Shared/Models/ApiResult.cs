using System.Collections;

namespace JobsWorker.Shared.Models
{
    public class ApiResult
    {
        public int ErrorCode { get; set; }
        public string Message { get; set; }
    }

    public class ApiResult<T> : ApiResult
    {
        public T? Result { get; set; }
    }

    public class BatchRemoveResult<T> : List<T>, IEnumerable<T>
    {

        public IEnumerable<ApiResult<T>> Results { get; set; }
    }
}
