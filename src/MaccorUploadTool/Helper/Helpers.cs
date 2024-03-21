using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorUploadTool.Helper
{
    internal class Helpers
    {
        public static DateTime DelphiTimeToDateTime(double delphiTime)
        {
            // scale it to 100nsec ticks.  Yes, we could do this as one expression
            // but this makes it a lot easier to read.

            // delphitime *= 864000000000L

            delphiTime *= 24; // hours since Delphi epoch
            delphiTime *= 60; // minutes since epoch
            delphiTime *= 60; // seconds since epoch
            delphiTime *= 1000; // milliseconds since epoch
            delphiTime *= 1000; // microseconds since epoch
            delphiTime *= 10; // 100 nsec ticks since epoch

            // Now, delphiTime is the number of ticks since 1899/12/30

            long time = 599264352000000000L; // 1/1/0001 --> 1899/12/30

            time += (long)delphiTime;

            // These are all *local* times, sadly
            return new DateTime(time, DateTimeKind.Local);
        }

    }
}
