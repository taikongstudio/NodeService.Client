using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MaccorUploadTool.Data
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DLLDataFileHeader
    {
        public ulong Size;//uint64
        public uint FileType; //1;// MacTest32; 2;// Indexed; 3;// ASCII; 4;// NG
        public uint SystemType;//;// int32;
        public uint SystemIDLen;// int32;
        public uint TestChan;// int32;
        public uint TestNameLen;// int32;
        public uint TestInfoLen;// int32;
        public uint ProcNameLen;// int32;
        public uint ProcDescLen;// int32;
        public float Mass;// single;
        public float Volume;// single;
        public float Area;// single;
        public float C_Rate;// single;
        public float V_Rate;// single;
        public float R_Rate;// single;
        public float P_Rate;// single;
        public float I_Rate;// single;
        public float E_Rate;// single;
        public float ParallelR;// single;
        public float VDivHiR;// single;
        public float VDivLoR;// single;
        public uint HeaderIndex;// int32;
        public uint LastRecNum;// int32;
        public uint TestStepNum;// int32;
        public double StartDateTime;// TDateTime;
        public float MaxV;// Single;
        public float MinV;// Single;
        public float MaxChI;// Single;
        public float MaxDisChI;// Single;
        public ushort AUXtot;// word;
        public ushort SMBtot;// word;
        public ushort CANtot;// word;
        public ushort EVChamberNum;// word;
        public byte HasDigIO;// boolean;
        public float MaxStepsPerSec;// single;
        public float MaxDataRate; //single
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DLLTimeData
    {
        public uint RecNum;//dword;
        public int CycleNumProc;//integer;
        public int HalfCycleNumCalc;//integer;
        public ushort StepNum;//word;
        public double DPtTime;//TDateTime;
        public double TestTime;//double;
        public double StepTime;//double;
        public double Capacity;//double;
        public double Energy;//double;
        public float Current;//single;
        public float Voltage;//single;
        public float ACZ;//single;
        public float DCIR;//single;
        public short MainMode;//Char;
        public byte Mode;//Byte;
        public byte EndCode;//Byte;
        public byte Range;//byte;
        public ulong GlobFlags;//uint64;
        public short HasVarData;//word;
        public short HasGlobFlags;//word;
        public short HasFRAData;//word;
        public short DigIO;//word;
        public long FRAStartTime;//TDateTime;
        public int FRAExpNum;//integer;

    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DllArrayItem
    {
        [FieldOffset(0)]
        public float V;
        [FieldOffset(4)]
        public float I;
    }



    //        TDLLScopeTrace=packed record
    //Samples: byte; //Number of samples in a ms
    //Reading: array[0..49] of packed record
    //V: single;
    //I: single;

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct DLLScopeTrace
    {
        [FieldOffset(0)]
        public byte Samples;
        [FieldOffset(1)]
        public fixed byte Array[50 * 8];


    }



}
