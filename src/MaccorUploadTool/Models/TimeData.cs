using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MaccorUploadTool.Data;
using MaccorUploadTool.Helper;

namespace MaccorUploadTool.Models
{
    public class TimeData
    {
        public string FilePath { get; set; }

        public string IPAddress { get; set; }

        public string DnsName { get; set; }

        public uint RecNum { get; set; }//dword;
        public int CycleNumProc { get; set; }//integer;
        public int HalfCycleNumCalc { get; set; }//integer;
        public ushort StepNum { get; set; }//word;
        public DateTime DPtTime { get; set; }//TDateTime;
        public double TestTime { get; set; }//double;
        public double StepTime { get; set; }//double;
        public double Capacity { get; set; }//double;
        public double Energy { get; set; }//double;
        public float Current { get; set; }//single;
        public float Voltage { get; set; }//single;
        public float ACZ { get; set; }//single;
        public float DCIR { get; set; }//single;
        public short MainMode { get; set; }//Char;
        public byte Mode { get; set; }//Byte;
        public byte EndCode { get; set; }//Byte;
        public byte Range { get; set; }//byte;
        public ulong GlobFlags { get; set; }//uint64;
        public short HasVarData { get; set; }//word;
        public short HasGlobFlags { get; set; }//word;
        public short HasFRAData { get; set; }//word;
        public short DigIO { get; set; }//word;
        public long FRAStartTime { get; set; }//TDateTime;
        public int FRAExpNum { get; set; }//integer;

        public string AsJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public void Init(DLLTimeData timeData)
        {
            RecNum = timeData.RecNum;//: dword;
            CycleNumProc = timeData.CycleNumProc;//: integer;
            HalfCycleNumCalc = timeData.HalfCycleNumCalc;//: integer;
            StepNum = timeData.StepNum;//: word;
            DPtTime = Helpers.DelphiTimeToDateTime(timeData.DPtTime);//: TDateTime;
            TestTime = timeData.TestTime;//: double;
            StepTime = timeData.StepTime;//: double;
            Capacity = timeData.Capacity;//: double;
            Energy = timeData.Energy;//: double;
            Current = timeData.Current;//: single;
            Voltage = timeData.Voltage;//: single;
            ACZ = timeData.ACZ;//: single;
            DCIR = timeData.DCIR;//: single;
            MainMode = timeData.MainMode;//: Char;
            Mode = timeData.Mode;//: Byte;
            EndCode = timeData.EndCode;//: Byte;
            Range = timeData.Range;//: byte;
            GlobFlags = timeData.GlobFlags;//: uint64;
            HasVarData = timeData.HasVarData;//: word;
            HasGlobFlags = timeData.HasGlobFlags;//: word;
            HasFRAData = timeData.HasFRAData;//: word;
            DigIO = timeData.DigIO;//: word;
            FRAStartTime = timeData.FRAStartTime;//: TDateTime;
            FRAExpNum = timeData.FRAExpNum;//: integer;
        }

    }
}
