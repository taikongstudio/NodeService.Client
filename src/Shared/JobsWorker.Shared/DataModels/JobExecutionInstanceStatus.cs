﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public enum JobExecutionInstanceStatus
    {
        Triggered,
        Started,
        Running,
        Finished,
        Failed
    }
}
