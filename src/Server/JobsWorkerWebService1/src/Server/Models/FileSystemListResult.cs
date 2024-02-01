

namespace JobsWorkerWebService.Server.Models
{
    public class FileSystemListResult
    {
        public int ErrorCode { get; set; }

        public string Message { get; set; }

        public IEnumerable<object> FileSystemObjects { get; set; }
    }
}
