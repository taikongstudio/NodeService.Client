namespace JobsWorkerWebService.Server.Models
{
    public class FileSystemListResult
    {
        public int ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public IEnumerable<FileSystemInfo> Items { get; set; }
    }
}
