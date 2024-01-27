using JobsWorkerNode.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNode.Interop.WindowInterop
{
    public delegate bool HwndEnumProc(Hwnd hwnd);

    public static class ProcessChildWindowsHelper
    {


        unsafe struct EnumChildWindowsParameters
        {
            public nint FunctionPtr;
        }

        private static unsafe int EnumChildWindowProc(nint hwnd, nint lparam)
        {
            //var length = NativeMethods.GetWindowTextLength(hwnd);
            //if (length == 0) return 1;
            //char* pChar = stackalloc char[length * 2];
            //NativeMethods.GetWindowTextW(hwnd, pChar, length * 2);
            //var str = new string(pChar, 0, length);
            //Console.WriteLine(str);
            ////Console.WriteLine(string.Join("-", str.ToCharArray()));
            ////Console.WriteLine(string.Join("-", "启动".ToCharArray()));
            //if (str == "启动" || str.Contains("启") && str.Contains("动"))
            //{
            //	Console.WriteLine($"Find window:{hwnd}");
            //	if (NativeMethods.IsWindowEnabled(hwnd))
            //	{
            //		const int BM_CLICK = 0x00F5;
            //		NativeMethods.SendMessage(hwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            //		Console.WriteLine($"Clicked:{hwnd}");
            //	}
            //	return 0;
            //}
            EnumChildWindowsParameters* enumChildWindowsParameters = (EnumChildWindowsParameters*)lparam;
            HwndEnumProc hwndEnumProc = Marshal.GetDelegateForFunctionPointer<HwndEnumProc>(enumChildWindowsParameters->FunctionPtr);

            return hwndEnumProc.Invoke(new Hwnd(hwnd)) ? 1 : 0;
        }

        public static unsafe void EnumChildWindows(this Process process, string buttonText, HwndEnumProc hwndEnumProc)
        {
            EnumChildWindowsParameters* enumChildWindowsParameters = (EnumChildWindowsParameters*)Marshal.AllocHGlobal(Marshal.SizeOf<EnumChildWindowsParameters>());
            enumChildWindowsParameters->FunctionPtr = Marshal.GetFunctionPointerForDelegate(hwndEnumProc);

            NativeMethods.EnumChildWindows(process.MainWindowHandle, &EnumChildWindowProc, (nint)enumChildWindowsParameters);

        }


    }
}
