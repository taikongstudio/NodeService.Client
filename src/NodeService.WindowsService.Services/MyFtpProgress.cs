using FluentFTP;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public class MyFtpProgress : IProgress<FtpProgress>
    {

        private Action<FtpProgress> _action;

        public MyFtpProgress(Action<FtpProgress> action)
        {
            _action = action;
        }

        public void Report(FtpProgress value)
        {
            _action?.Invoke(value);
        }
    }
}
