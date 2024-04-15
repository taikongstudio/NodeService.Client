using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LANDCexSharp
{
    public enum VARENUM:int
    {
        VT_EMPTY = 0,
        VT_NULL = 1,
        VT_I2 = 2,
        VT_I4 = 3,
        VT_R4 = 4,
        VT_R8 = 5,
        VT_CY = 6,
        VT_DATE = 7,
        VT_BSTR = 8,
        VT_DISPATCH = 9,
        VT_ERROR = 10,
        VT_BOOL = 11,
        VT_VARIANT = 12,
        VT_UNKNOWN = 13,
        VT_DECIMAL = 14,
        VT_I1 = 16,
        VT_UI1 = 17,
        VT_UI2 = 18,
        VT_UI4 = 19,
        VT_I8 = 20,
        VT_UI8 = 21,
        VT_INT = 22,
        VT_UINT = 23,
        VT_VOID = 24,
        VT_HRESULT = 25,
        VT_PTR = 26,
        VT_SAFEARRAY = 27,
        VT_CARRAY = 28,
        VT_USERDEFINED = 29,
        VT_LPSTR = 30,
        VT_LPWSTR = 31,
        VT_RECORD = 36,
        VT_INT_PTR = 37,
        VT_UINT_PTR = 38,
        VT_FILETIME = 64,
        VT_BLOB = 65,
        VT_STREAM = 66,
        VT_STORAGE = 67,
        VT_STREAMED_OBJECT = 68,
        VT_STORED_OBJECT = 69,
        VT_BLOB_OBJECT = 70,
        VT_CF = 71,
        VT_CLSID = 72,
        VT_VERSIONED_STREAM = 73,
        VT_BSTR_BLOB = 0xfff,
        VT_VECTOR = 0x1000,
        VT_ARRAY = 0x2000,
        VT_BYREF = 0x4000,
        VT_RESERVED = 0x8000,
        VT_ILLEGAL = 0xffff,
        VT_ILLEGALMASKED = 0xfff,
        VT_TYPEMASK = 0xfff
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct Variant
    {
        [FieldOffset(0)]
        public VARENUM Type;

        [FieldOffset(8)]
        public byte UInt8Value;
        [FieldOffset(8)]
        public sbyte Int8Value;
        [FieldOffset(8)]
        public short Int16Value;
        [FieldOffset(8)]
        public ushort UInt16Value;
        [FieldOffset(8)]
        public int Int32Value;
        [FieldOffset(8)]
        public uint UInt32Value;
        [FieldOffset(8)]
        public long Int64Value;
        [FieldOffset(8)]
        public ulong UInt64Value;
        [FieldOffset(8)]
        public double DoubleValue;
        [FieldOffset(8)]
        public float SingleValue;
        [FieldOffset(8)]
        public IntPtr PtrValue;
    }
}
