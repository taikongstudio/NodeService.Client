using JobsWorker.Shared.MessageQueues.Models;
using JobsWorker.Shared.Models;

namespace JobsWorkerNodeService.Models
{
    public class UploadFileToFtpServerRequest : RequestMessage
    {
        public string FileName { get; set; }
    }
}
