using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pocket
{
    public class AudioReadyData
    {
        public int UdpPort { get; set; }
        public int SampleRate { get; set; } = 48000; // High quality
        public int Channels { get; set; } = 2; // Stereo
        public int BitsPerSample { get; set; } = 24; // High quality
    }
}
