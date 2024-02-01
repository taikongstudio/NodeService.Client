using FluentFTP;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs
{
    public class MyFtpProgress : IProgress<FtpProgress>
    {
        public ILogger Logger { get; private set; }

        public MyFtpProgress(ILogger logger)
        {
            this.Logger = logger;
        }

        public void Report(FtpProgress value)
        {
           
        }
    }
}
