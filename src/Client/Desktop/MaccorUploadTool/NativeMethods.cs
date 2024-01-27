using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MaccorUploadTool.Data;

namespace MaccorUploadTool
{
    internal unsafe static class NativeMethods
    {
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int OpenDataFile(string FileName);//: int32;

        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int OpenDataFileASCII(string FileName);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int CloseDataFile(int Handle);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int ResetDataFile(int Handle);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int LoadNextDataFileHeader(int Handle);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static unsafe extern int GetDataFileHeader(int Handle, ref DLLDataFileHeader ptr);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetSystemID(int Handle, StringBuilder SystemID, uint len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int GetSystemIDASCII(int Handle, string SystemID, int len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetTestInfo(int Handle, StringBuilder PTestInfo, uint len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int GetTestInfoASCII(int Handle, string PTestInfo, int len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetProcName(int Handle, StringBuilder SystemID, uint len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int GetProcNameASCII(int Handle, string SystemID, int len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetProcDesc(int Handle, StringBuilder PProcDesc, uint len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int GetProcDescASCII(int Handle, string PProcDesc, int len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int SaveTestProcedureToFile(int Handle, string FileName);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int SaveTestProcedureToFileASCII(int Handle, string FileName);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetAuxUnits(int Handle, int AUXnum, string PAUXUnit);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int GetAuxUnitsASCII(int Handle, int AUXnum, string PAUXUnit);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetSMBUnits(int Handle, int SMBnum, string PSMBUnit);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int GetSMBUnitsASCII(int Handle, int SMBnum, string PSMBUnit);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int LoadAndGetNextTimeData(int Handle, ref DLLTimeData timeData);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetAuxData(int Handle, int AUXnum, float AUXval);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetSMBData(int Handle, int SMBnum, short SMBval, float SMBvalFloat);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetSMBDataWideChar(int Handle, short SMBnum, short SMBval, float SMBvalFloat, string PSMBValStr, int len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern int GetSMBDataASCII(int Handle, short SMBnum, short SMBval, float SMBvalFloat, string PSMBValStr, int len);//: int32;
        [DllImport("MacReadDataFileLIB.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int GetScopeTrace(int Handle, ref DLLScopeTrace pointer);//: int32;




    }
}
