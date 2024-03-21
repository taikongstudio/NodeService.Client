using NodeService.WindowsService.Services.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services.Helpers
{
    public class Hwnd
    {
        private nint _hwnd;
        public Hwnd(nint hwnd)
        {
            _hwnd = hwnd;
        }

        public nint Handle { get { return _hwnd; } }

        public string Text
        {
            get
            {
                return getWindowTextImpl();

            }
        }

        public bool IsEnabeld
        {
            get
            {
                return NativeMethods.IsWindowEnabled(_hwnd);
            }
        }

        private unsafe string getWindowTextImpl()
        {
            var length = NativeMethods.GetWindowTextLength(_hwnd);
            if (length == 0) return string.Empty;
            char* pChar = stackalloc char[length * 2];
            NativeMethods.GetWindowTextW(_hwnd, pChar, length * 2);
            var str = new string(pChar, 0, length);
            return str;
        }

        public void SendMessage(int msg)
        {
            NativeMethods.SendMessage(_hwnd, (uint)msg, nint.Zero, nint.Zero);
        }

    }
}
