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


//----------------------------------------------------ADPCM编解码----------------------------------------------------------------
    class ADPCMState
    {
        internal Int16 predicated;
        internal int index;
    }

    public class ADPCMDecoder
    {
        internal static readonly sbyte[] indexTable = { -1, -1, -1, -1, 2, 4, 6, 8, /* Table of index changes */
                                               -1, -1, -1, -1, 2, 4, 6, 8 };

        internal static readonly Int16[] stepsizeTable = { 7, 8, 9, 10, 11, 12, 13, 14, /* quantizer lookup table */
            16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
            130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658,
            724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024,
            3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635,
            13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767 };

        ADPCMState State;
        Int16 []Buffer;
        int WriteIndex;

        public ADPCMDecoder(int PCMBufferSize)
        {
            State = new ADPCMState();
            Buffer = new Int16[PCMBufferSize];
            Reset();
        }

        public void Reset()
        {
            State.predicated = 0;
            State.index = 0;
            WriteIndex = 0;
        }

        public event EventHandler<Int16 []> PCMOutput;

        void Update(byte sample)
        {
            int diff;
            int step_size = stepsizeTable[State.index];
            int predicated = State.predicated;

            /* compute new sample estimate predictedSample */
            diff = ((sample & 0x7) * step_size) >> 2; // calculate difference = (newSample + 1/2) * stepsize/4 
            diff += step_size >> 3;
            if ((sample & 0x8) != 0)
                diff = -diff;

            /* adjust predicted sample based on calculated difference: */
            predicated += diff;
            if (predicated > 32767)
                predicated = 32767;
            else if (predicated < -32768)
                predicated = -32768;
            State.predicated = (Int16)predicated;

            // update stepsize
            State.index += indexTable[sample];
            if (State.index < 0) /* check for index underflow */
                State.index = 0;
            else if (State.index > stepsizeTable.Length - 1) /* check for index overflow */
                State.index = stepsizeTable.Length - 1;
        }

        void Decode0(byte sample)
        {
            Update(sample);
            if (WriteIndex >= Buffer.Length)
            {
                //数据满帧，将数据传出
                PCMOutput.Invoke(this, Buffer);
                WriteIndex = 0;
            }
            Buffer[WriteIndex] = State.predicated;
            WriteIndex++;
        }

        public void Decode(byte data)
        {
            Decode0((byte)(data >> 4));
            Decode0((byte)(data & 0xf));
        }

        public void Decode (byte[] data)
        {
            foreach (var x in data) Decode(x);
        }
    }

    public class ADPCMEncoder
    {
        ADPCMState State;
        Int16[] Buffer;
        int WriteIndex;

        public ADPCMEncoder(int PCMBufferSize)
        {
            State = new ADPCMState();
            Buffer = new Int16[PCMBufferSize];
            Reset();
        }

        public void Reset()
        {
            State.predicated = 0;
            State.index = 0;
            WriteIndex = 0;
        }

        public event EventHandler<Int16[]> PCMOutput;

        void Update(byte sample)
        {
            int diff;
            int step_size = ADPCMDecoder.stepsizeTable[State.index];
            int predicated = State.predicated;

            /* compute new sample estimate predictedSample */
            diff = ((sample & 0x7) * step_size) >> 2; // calculate difference = (newSample + 1/2) * stepsize/4 
            diff += step_size >> 3;
            if ((sample & 0x8) != 0)
                diff = -diff;

            /* adjust predicted sample based on calculated difference: */
            predicated += diff;
            if (predicated > 32767)
                predicated = 32767;
            else if (predicated < -32768)
                predicated = -32768;
            State.predicated = (Int16)predicated;

            // update stepsize
            State.index += ADPCMDecoder.indexTable[sample];
            if (State.index < 0) /* check for index underflow */
                State.index = 0;
            else if (State.index > ADPCMDecoder.stepsizeTable.Length - 1) /* check for index overflow */
                State.index = ADPCMDecoder.stepsizeTable.Length - 1;
        }

        void Decode0(byte sample)
        {
            Update(sample);
            if (WriteIndex >= Buffer.Length)
            {
                PCMOutput.Invoke(this, Buffer);
                WriteIndex = 0;
            }
            Buffer[WriteIndex] = State.predicated;
            WriteIndex++;
        }

        public void Decode(byte data)
        {
            Decode0((byte)(data >> 4));
            Decode0((byte)(data & 0xf));
        }

        public void Decode(byte[] data)
        {
            foreach (var x in data) Decode(x);
        }
    }  
}
