using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Media;

using INGdemo.Lib;

[assembly: Xamarin.Forms.Dependency(typeof(INGdemo.Lib.Droid.AudioServiceImpl))]
namespace INGdemo.Lib.Droid
{
    class AudioServiceImpl : IPCMAudio
    {
        AudioTrack Track;

        public void Play(int samplingRate)
        {
            if (Track != null)
                Stop();

            Track = new AudioTrack(
                new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Media)
                    .Build(),
                new AudioFormat.Builder()
                    .SetEncoding(Android.Media.Encoding.Pcm16bit)
                    .SetSampleRate(samplingRate)
                    .SetChannelMask(ChannelOut.Mono)
                    .Build(),
                samplingRate * 2 / 5,
                AudioTrackMode.Stream,
                1);
            Track.Play();
        }

        public void Stop()
        {
            if (Track != null)
            {
                Track.Stop();
                Track.Dispose();
                Track = null;
            }            
        }

        ~AudioServiceImpl()
        {
            Stop();
        }

        public bool Write(short[] samples)
        {
            if (Track == null)
                return false;

            try
            {                
                Track.Write(samples, 0, samples.Length);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }
}