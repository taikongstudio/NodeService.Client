using JobsWorker.Shared;
using JobsWorker.Shared.MessageQueues.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerWebService.GrpcServices.Models
{
    public class NodeConfigTemplateChangedMessage : RequestMessage<ConfigurationChangedReport>
    {
    }
}
