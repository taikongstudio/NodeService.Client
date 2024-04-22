using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.ServiceHost.Interop
{
    internal class NativeMethods
    {

        [DllImport("user32.dll")]
        public static extern nint FindWindowEx(nint hwnd, nint childAfter, string className, string windowName);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll")]
        public static extern unsafe nint GetWindowTextW(
  nint hWnd,
  char* stringBuilder,
   int nMaxCount);

        [DllImport("user32.dll")]
        public static extern unsafe nint EnumChildWindows(
  nint hWndParent,
 delegate*<nint, nint, int> proc,
  nint lParam
);
        [DllImport("user32.dll")]
        public static extern unsafe nint SendMessage(
  nint hWnd,
  uint Msg,
  nint wParam,
  nint lParam
);

        [DllImport("user32.dll")]
        public static extern unsafe bool IsWindowEnabled(nint hWnd);


        [DllImport("kernel32.dll")]
        public static extern int GetLastError();


        [DllImport("kernel32.dll")]
        public static extern int OpenProcessToken(
 nint ProcessHandle,
  int DesiredAccess,
  out nint TokenHandle
);

        [DllImport("kernel32.dll")]
        public static extern nint GetCurrentProcess();


        public enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        public enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        [DllImport("kernel32.dll")]
        public static extern int DuplicateTokenEx(
  nint hExistingToken,
  int dwDesiredAccess,
   nint lpTokenAttributes,
  SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
   TOKEN_TYPE TokenType,
  out nint phNewToken
);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(nint hObject);
    }
}
