using JobsWorkerWebService.Server.Services;

namespace JobsWorkerWebService.Server.FileSystemServices
{
    public abstract class FileSystemResponse : IKeyedObject
    {
        public Guid Id { get; set; }
    }

}
