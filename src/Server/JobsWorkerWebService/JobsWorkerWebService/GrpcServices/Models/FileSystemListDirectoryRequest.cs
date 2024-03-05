using JobsWorker.Shared;
using JobsWorker.Shared.MessageQueues.Models;
using System.Globalization;

namespace JobsWorkerWebService.GrpcServices.Models
{
    public class FileSystemListDirectoryRequest : RequestMessage<FileSystemListDirectoryReq>
    {

    }
}
