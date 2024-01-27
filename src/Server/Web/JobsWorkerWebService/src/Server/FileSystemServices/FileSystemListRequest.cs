namespace JobsWorkerWebService.Server.FileSystemServices
{
    public class FileSystemListRequest : FileSystemRequest
    {
        public string Path { get; set; }

        public string SearchPattern { get; set; }

        public bool IncludeSubDirectories { get; set; }

        public int Timeout { get; set; }
    }
}
