using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService
{
    public class Constants
    {
        public const string Version = "45DCED93-3C9D-48C6-B97B-AC3BA3D8F224";

        public const string ServiceProcessWindowsService = "NodeService.WindowsService";

        public const string ServiceProcessUpdateService = "NodeService.UpdateService";

        public const string ServiceProcessWorkerService = "NodeService.WorkerService";


        public const string ProcessChannelInfoDictionary = nameof(ProcessChannelInfoDictionary);
    }
}
