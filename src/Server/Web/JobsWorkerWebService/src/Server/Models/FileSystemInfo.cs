namespace JobsWorkerWebService.Server.Models
{
    public class FileSystemInfo
    {
        public string Name { get; set; }

        public string FullName { get; set; }

        public string Type { get; set; }

        public long Length { get; set; }

        public DateTime CreationTime { get; set; }

        public DateTime LastWriteTime { get; set; }
    }
}
