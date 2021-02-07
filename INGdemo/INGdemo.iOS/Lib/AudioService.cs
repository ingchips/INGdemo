using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

using Foundation;
using UIKit;
using AudioToolbox;

using INGdemo.Lib;

[assembly: Xamarin.Forms.Dependency(typeof(INGdemo.Lib.iOS.AudioServiceImpl))]
namespace INGdemo.Lib.iOS
{
    class AudioServiceImpl : IPCMAudio
    {
        OutputAudioQueue OutputQueue;
        
        int MaxBufferCount = 10;
        int BufferSizeInByte = 512 * 2;
        int channels = 1;
        int CurBuffIndex = 0;
        int BufReadyNum = 0;
        static readonly int MIN_READY_BUF = 3;

        internal class AudioBuffer
        {
            public IntPtr Data { get; set; }
            public bool IsInUse { get; set; }
        }

        List<AudioBuffer> Buffers;
        IntPtr RawBuffer;
        int RawCur;
        AudioStreamPacketDescription[] Descriptions = new AudioStreamPacketDescription[1];

        void Init(int samplingRate)
        {
            var interleaved = false;
            var desc = new AudioStreamBasicDescription()
            {
                SampleRate = samplingRate,
                Format = AudioFormatType.LinearPCM,
                FormatFlags = AudioFormatFlags.LinearPCMIsSignedInteger | AudioFormatFlags.LinearPCMIsPacked,
                BitsPerChannel = 16,
                ChannelsPerFrame = channels,
                BytesPerFrame = 2 * (interleaved ? channels : 1),
                BytesPerPacket = 2,
                FramesPerPacket = 1,
            };

            Descriptions[0].StartOffset = 0;
            Descriptions[0].DataByteSize = BufferSizeInByte;

            OutputQueue = new OutputAudioQueue(desc);
            if (OutputQueue == null)
                return;

            // AudioQueue.FillAudioData write "raw samples" into AudioQueueBuffer
            RawBuffer = Marshal.AllocHGlobal(BufferSizeInByte);

            Buffers = new List<AudioBuffer>();
            for (int i = 0; i < MaxBufferCount; i++)
            {
                IntPtr outBuffer;
                OutputQueue.AllocateBuffer(BufferSizeInByte, out outBuffer);
                Buffers.Add(new AudioBuffer()
                {
                    Data = outBuffer
                });
            }

            CurBuffIndex = 0;
            BufReadyNum = 0;

            OutputQueue.BufferCompleted += OutputQueue_BufferCompleted;
        }

        private void OutputQueue_BufferCompleted(object sender, BufferCompletedEventArgs e)
        {
            var index = Buffers.FindIndex((x) => x.Data == e.IntPtrBuffer);
            var buf = Buffers[index];

            lock (buf)
            {
                buf.IsInUse = false;
            }
            Interlocked.Decrement(ref BufReadyNum);

            if (BufReadyNum < MIN_READY_BUF)
                OutputQueue.Pause();
        }

        private void TryStart()
        {
            if (BufReadyNum > MIN_READY_BUF)
                OutputQueue.Start();
        }

        public bool Write(Int16[] samples)
        {
            if (OutputQueue == null)
                return false;

            int off = 0;
            while (samples.Length - off > 0)
            {
                int rem = (BufferSizeInByte - RawCur) / 2;

                if (rem < 1)
                {
                    var buff = Buffers[CurBuffIndex];
                    if (buff.IsInUse)
                    {
                        RawCur = 0;
                        return false;
                    }
                    lock (buff)
                    {
                        buff.IsInUse = true;
                    }

                    AudioQueue.FillAudioData(buff.Data, 0, RawBuffer, 0, BufferSizeInByte);
                    OutputQueue.EnqueueBuffer(buff.Data, BufferSizeInByte, null);
                    
                    Interlocked.Increment(ref BufReadyNum);
                    TryStart();

                    RawCur = 0;
                    rem = (BufferSizeInByte - RawCur) / 2;
                    CurBuffIndex = (CurBuffIndex + 1) % Buffers.Count;                    
                }

                int l = Math.Min(rem, samples.Length - off);
                Marshal.Copy(samples, off, RawBuffer + RawCur, l);
                off += l;
                RawCur += l * 2;
            }

            return true;
        }

        public void Play(int samplingRate)
        {
            if (OutputQueue != null)
                Stop();

            Init(samplingRate);
            if (OutputQueue != null)
                OutputQueue.Start();
        }

        public void Stop()
        {
            if (OutputQueue == null)
                return;
            OutputQueue.Stop(true);
            OutputQueue.Reset();
            foreach (var buf in Buffers)
                OutputQueue.FreeBuffer(buf.Data);
            Buffers = null;
            Marshal.FreeHGlobal(RawBuffer);
            OutputQueue.Dispose();
            OutputQueue = null;
        }

        ~AudioServiceImpl()
        {
            Stop();
        }
    }
}