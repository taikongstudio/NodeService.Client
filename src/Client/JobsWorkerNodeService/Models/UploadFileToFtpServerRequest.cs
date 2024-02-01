using JobsWorker.Shared.MessageQueue.Models;

namespace JobsWorkerNodeService.Models
{
    public class UploadFileToFtpServerRequest : RequestMessage
    {
        public string FileName { get; set; }

        public string RemotePath { get; set; }

        public string FtpConfig { get; set; }
    }
}
