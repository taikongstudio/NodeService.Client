using NodeService.ServiceHost.Interop;
using System.Runtime.InteropServices;

namespace NodeService.ServiceHost.Helpers
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
