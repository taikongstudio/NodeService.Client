using JobsWorkerWebService.Server.Services;

namespace JobsWorkerWebService.Server.FileSystemServices
{
    public abstract class FileSystemRequest : IKeyedObject
    {
        public Guid Id { get; set; }

    }
}
