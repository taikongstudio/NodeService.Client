using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MaccorUploadTool;
using MaccorUploadTool.Data;
using MaccorUploadTool.Helper;

namespace MaccorUploadTool.Models
{
    public struct DataFileHeader
    {
        public string FilePath { get; set; }

        public string IPAddress { get; set; }

        public string DnsName { get; set; }

        public ulong Size { get; private set; } //uint64
        public uint FileType { get; private set; }  //1;// MacTest32; 2;// Indexed; 3;// ASCII; 4;// NG
        public uint SystemType { get; private set; } //;// int32;
        public string SystemID { get; private set; }// int32;
        public uint TestChan { get; private set; }// int32;
        public string TestName { get; private set; }// int32;
        public string TestInfo { get; private set; }// int32;
        public string ProcName { get; private set; }// int32;
        public string ProcDesc { get; private set; }
        public float Mass { get; private set; } // single;
        public float Volume { get; private set; } // single;
        public float Area { get; private set; } // single;
        public float C_Rate { get; private set; } // single;
        public float V_Rate { get; private set; } // single;
        public float R_Rate { get; private set; } // single;
        public float P_Rate { get; private set; } // single;
        public float I_Rate { get; private set; } // single;
        public float E_Rate { get; private set; } // single;
        public float ParallelR { get; private set; } // single;
        public float VDivHiR { get; private set; } // single;
        public float VDivLoR { get; private set; } // single;
        public uint HeaderIndex { get; private set; } // int32;
        public uint LastRecNum { get; private set; } // int32;
        public uint TestStepNum { get; private set; } // int32;
        public DateTime StartDateTime { get; private set; } // TDateTime;
        public float MaxV { get; private set; } // Single;
        public float MinV { get; private set; } // Single;
        public float MaxChI { get; private set; } // Single;
        public float MaxDisChI { get; private set; } // Single;
        public ushort AUXtot { get; private set; } // word;
        public ushort SMBtot { get; private set; } // word;
        public ushort CANtot { get; private set; } // word;
        public ushort EVChamberNum { get; private set; } // word;
        public byte HasDigIO { get; private set; } // boolean;
        public float MaxStepsPerSec { get; private set; } // single;
        public float MaxDataRate { get; private set; } //single

        public string AsJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        private static void ClearAndResize(StringBuilder stringBuilder, uint newCapacity)
        {
            stringBuilder.Clear();
            if (stringBuilder.Capacity < newCapacity)
            {
                stringBuilder.Capacity = (int)newCapacity;
            }
        }


        public void Init(int handle, DLLDataFileHeader dllHeaderData)
        {
            Size = dllHeaderData.Size;//                   : uint64;
            FileType = dllHeaderData.FileType;//: int32; //1: MacTest32; 2: Indexed; 3: ASCII; 4: NG
            SystemType = dllHeaderData.SystemType;//: int32;
                                                  //SystemIDLen = dllHeaderData.SystemIDLen;//: int32;

            StringBuilder sb = new StringBuilder(256);
            {
                ClearAndResize(sb, dllHeaderData.SystemIDLen);
                int ret = NativeMethods.GetSystemID(handle, sb, dllHeaderData.SystemIDLen);
                if (ret == 0)
                {
                    SystemID = sb.ToString();
                }
            }
            //TestNameLen = dllHeaderData.TestNameLen;//: int32;

            {
                TestName = string.Empty;
            }

            //TestInfoLen = dllHeaderData.TestInfoLen;//: int32;

            {
                ClearAndResize(sb, dllHeaderData.TestInfoLen);
                int ret = NativeMethods.GetTestInfo(handle, sb, dllHeaderData.TestInfoLen);
                if (ret == 0)
                {
                    TestInfo = sb.ToString();
                }
            }


            //ProcNameLen = dllHeaderData.ProcNameLen; //: int32;

            {
                ClearAndResize(sb, dllHeaderData.ProcNameLen);
                int ret = NativeMethods.GetProcName(handle, sb, dllHeaderData.ProcNameLen);
                if (ret == 0)
                {
                    ProcName = sb.ToString();
                }
            }

            //ProcDescLen = dllHeaderData.ProcDescLen;//: int32;

            {
                ClearAndResize(sb, dllHeaderData.ProcDescLen);
                int ret = NativeMethods.GetProcDesc(handle, sb, dllHeaderData.ProcDescLen);
                if (ret == 0)
                {
                    ProcDesc = sb.ToString();
                }
            }




            TestChan = dllHeaderData.TestChan;//: int32;
            Mass = dllHeaderData.Mass;//: single;
            Volume = dllHeaderData.Volume;//: single;
            Area = dllHeaderData.Area;//: single;
            C_Rate = dllHeaderData.C_Rate;//: single;
            V_Rate = dllHeaderData.V_Rate;//: single;
            R_Rate = dllHeaderData.R_Rate;//: single;
            P_Rate = dllHeaderData.P_Rate;//: single;
            I_Rate = dllHeaderData.I_Rate;//: single;
            E_Rate = dllHeaderData.E_Rate;//: single;
            ParallelR = dllHeaderData.ParallelR;//: single;
            VDivHiR = dllHeaderData.VDivHiR;//: single;
            VDivLoR = dllHeaderData.VDivLoR;//: single;
            HeaderIndex = dllHeaderData.HeaderIndex;//: int32;
            LastRecNum = dllHeaderData.LastRecNum;//: int32;
            TestStepNum = dllHeaderData.TestStepNum;//: int32;
            StartDateTime = Helpers.DelphiTimeToDateTime(dllHeaderData.StartDateTime);//: TDateTime;
            MaxV = dllHeaderData.MaxV;//: Single;
            MinV = dllHeaderData.MinV;//: Single;
            MaxChI = dllHeaderData.MaxChI;//: Single;
            MaxDisChI = dllHeaderData.MaxDisChI;//: Single;
            AUXtot = dllHeaderData.AUXtot;//: word;
            SMBtot = dllHeaderData.SMBtot;//: word;
            CANtot = dllHeaderData.CANtot;//: word;
            EVChamberNum = dllHeaderData.EVChamberNum;//: word;
            HasDigIO = dllHeaderData.HasDigIO;//: boolean;
            MaxStepsPerSec = dllHeaderData.MaxStepsPerSec;//: single;
            MaxDataRate = dllHeaderData.MaxDataRate;//: single;
        }

    }
}
