using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace INGdemo.Lib
{
    public interface IPCMAudio
    {
        bool Write(short[] samples);
        void Play(int samplingRate);
        void Stop();
    }

    public abstract class BasicPCMDecoder
    {
        Int16 []Buffer;
        int WriteIndex;

        public BasicPCMDecoder(int PCMBufferSize)
        {
            Buffer = new Int16[PCMBufferSize];
        }

        protected void EmitSample(short sample)
        {
            if (WriteIndex >= Buffer.Length)
            {
                PCMOutput.Invoke(this, Buffer);
                WriteIndex = 0;
            }
            Buffer[WriteIndex] = sample;
            WriteIndex++;
        }

        protected void EmitSamples(short []buffer, int start, int len)
        {
            for (int i = start; i < start + len; i++)
            {
                EmitSample(buffer[i]);
            }
        }

        public virtual void Reset()
        {
            WriteIndex = 0;
        }

        abstract public void Decode(byte[] data);

        public event EventHandler<Int16 []> PCMOutput;
    }
}
