

using JobsWorkerWebService.GrpcServices;

namespace JobsWorkerWebService.Server.FileSystemServices
{
    public class FileSystemListResponse : FileSystemResponse
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public IEnumerable<FileSystemObject> FileSystemObjects { get; set; }
    }
}
