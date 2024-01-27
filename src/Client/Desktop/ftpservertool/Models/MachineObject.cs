using FluentFTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftpservertool.Models
{
    public enum MachineOperation
    {
        None,
        DeleteMachineDirectory,
        MigrateToOtherServer,
        NeedConfig,
    }

    internal class MachineObject
    {
        public FtpClient FtpClient { get; set; }

        public string MachineDir { get; set; }

        public string LogsPath { get; set; }

        public bool HasTodayJobWorkerLogsPath { get; set; }

        public bool HasTodayShouhuLogsPath { get; set; }

        public bool IsRecordInMySql { get; set; }

        public bool IsConfiged { get; set; }
        public string Name { get; internal set; }

        public MySqlConfig MySqlConfig { get; set; }

        public MachineOperation Operation {  get; set; }
    }
}
