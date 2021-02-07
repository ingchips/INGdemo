using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

using INGdemo.Lib;

[assembly: Xamarin.Forms.Dependency(typeof(INGdemo.UWP.Lib.AudioServiceImpl))]
namespace INGdemo.UWP.Lib
{
    class AudioServiceImpl : IPCMAudio
    {
        private AudioGraph graph;
        private AudioDeviceOutputNode deviceOutputNode;
        private AudioFrameInputNode frameInputNode;
        private uint SamplingRate;
        private int FrameCompleted;
        private int FrameAdded;

        // Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array in an AudioFrame
        [ComImport]
        [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        unsafe interface IMemoryBufferByteAccess
        {
            void GetBuffer(out byte* buffer, out uint capacity);
        }

        private async Task CreateAudioGraph(uint samplingRate)
        {
            // Create an AudioGraph with default settings
            var encoding = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);
            encoding.Audio = AudioEncodingProperties.CreatePcm(samplingRate, 1, 16);
            var settings = new AudioGraphSettings(AudioRenderCategory.Speech);
            settings.EncodingProperties = encoding.Audio;
            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            if (result.Status != AudioGraphCreationStatus.Success)
                return;

            graph = result.Graph;
            graph.EncodingProperties.SampleRate = samplingRate;

            // Create a device output node
            CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await graph.CreateDeviceOutputNodeAsync();
            if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                return;

            deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

            // Create the FrameInputNode at the same format as the graph, except explicitly set mono.
            AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;
            nodeEncodingProperties.ChannelCount = 1;
            frameInputNode = graph.CreateFrameInputNode(nodeEncodingProperties);
            frameInputNode.AddOutgoingConnection(deviceOutputNode);

            // Initialize the Frame Input Node in the stopped state
            frameInputNode.Stop();

            frameInputNode.AudioFrameCompleted += FrameInputNode_AudioFrameCompleted;
            //frameInputNode.QuantumStarted += node_QuantumStarted;

            // Start the graph since we will only start/stop the frame input node
            graph.Start();
        }

        private void FrameInputNode_AudioFrameCompleted(AudioFrameInputNode sender, AudioFrameCompletedEventArgs args)
        {
            FrameCompleted++;
        }

        unsafe private AudioFrame GenerateAudioData(Int16[] samples)
        {
            uint bufferSize = (uint)samples.Length * sizeof(Int16);
            AudioFrame frame = new Windows.Media.AudioFrame(bufferSize);

            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                byte* dataInBytes;
                uint capacityInBytes;
                Int16* dataIn;

                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                // Cast to float since the data we are generating is float
                dataIn = (Int16*)dataInBytes;
                for (var i = 0; i < samples.Length; i++)
                    dataIn[i] = samples[i];
            }

            return frame;
        }

        public bool Write(Int16[] samples)
        {
            if ((graph == null)) return true;
            frameInputNode.AddFrame(GenerateAudioData(samples));
            FrameAdded++;
            if (FrameAdded - FrameCompleted >= 5)
                frameInputNode.Start();
            return true;
        }

        async public void Play(int samplingRate)
        {
            Stop();
            FrameAdded = 0;
            FrameCompleted = 0;
            SamplingRate = (uint)samplingRate;
            await CreateAudioGraph(SamplingRate);            
        }

        public void Stop()
        {
            if (graph == null) return;
            graph.Stop();
            graph.Dispose();
            graph = null;
        }
    }
}
