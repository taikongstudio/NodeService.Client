using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Models
{
    public class UploadFileToFtpServerJobOptions
    {
        public List<string> ftpUploadConfigNames { get; set; } = new List<string>();
    }
}
