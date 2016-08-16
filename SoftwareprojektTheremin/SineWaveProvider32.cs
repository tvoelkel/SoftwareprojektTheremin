using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftwareprojektTheremin
{
    class SineWaveProvider32 : NAudio.Wave.WaveProvider32
    {
        public SineWaveProvider32()
        {
            freq = 1000;
            amp = 0.25f;
        }

        public float Frequency {get; set;}
        public float Amplitude {get; set;}
        private float freq, amp, lastFrequency;
        private int sample = 0;
        public override int Read(float[] buffer, int offset, int sampleCount)
        {
 	        int sampleRate = WaveFormat.SampleRate;
            amp = Amplitude;

            if (amp < 0f)
                amp = 0.0f;
            if (amp > 1f)
                amp = 1f;

            if (freq < 0)
                freq = 0;

            for (int n = 0; n < sampleCount; n++)
            {
                freq = Frequency;
                if (Frequency != lastFrequency)
                {
                    freq = ((sampleCount - n - 1) * lastFrequency + Frequency) / (sampleCount - n);
                    lastFrequency = freq;
                }

                buffer[n + offset] = (float)(amp * Math.Sin((2 * Math.PI * sample * freq) / sampleRate));
                sample++;

                if (sample >= sampleRate)
                {
                    sample = 0;
                }
            }
            return sampleCount;
        }

    }
}
