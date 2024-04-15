using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LANDCexSharp
{
    public class BriefInfoReader
    {
        private nint _handle;

        public BriefInfoReader(string fileName)
        {
            this._handle = NativeMethods.LoadBriefInfo(fileName);
        }

        public unsafe BriefInfo Read()
        {
            BriefInfo briefInfo = new BriefInfo();
            byte boxNo = 0;
            byte channel = 0;
            int downId = 0;
            short downVersion = 0;
            if (NativeMethods.GetChannel(_handle, out boxNo, out channel, out downId, out downVersion))
            {
                briefInfo.BoxNo = boxNo;
                briefInfo.Channel = channel;
                briefInfo.DownId = downId;
                briefInfo.DownVersion = downVersion;
            }
            var startTimeStamp = NativeMethods.GetStartTime(_handle);
            var endTimeStamp = NativeMethods.GetEndTime(_handle);
            briefInfo.StartTime = DateTimeOffset.FromUnixTimeSeconds(startTimeStamp).DateTime;
            briefInfo.EndTime = DateTimeOffset.FromUnixTimeSeconds(endTimeStamp).DateTime;
            briefInfo.IsTimeHighResolution = NativeMethods.IsTimeHighResolution(_handle);
            Variant variant = default;
            if (NativeMethods.GetFormationName(_handle, out variant))
            {
                briefInfo.FormationName = Marshal.PtrToStringBSTR(variant.PtrValue);
            }
            NativeMethods.GetBattNo(_handle, &variant);
            briefInfo.BattNo = Marshal.PtrToStringBSTR(variant.PtrValue);
            NativeMethods.GetChlDataFullPath((byte)briefInfo.BoxNo, (byte)briefInfo.Channel, &variant);
            return briefInfo;
        }

    }
}
