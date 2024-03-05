using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorDataUpload.Models
{
    public struct Sample
    {
        public float V { get; set; }

        public float I { get; set; }
    }


    public class ScopeTrace
    {
        public ScopeTrace()
        {
            SamplesCount = 0;
            Samples = Array.Empty<Sample>();
        }

        public ScopeTrace(int samplesCount, Sample[] samples)
        {
            SamplesCount = samplesCount;
            Samples = samples;
        }

        public int SamplesCount { get; private set; }

        public Sample[] Samples { get; private set; }

    }
}