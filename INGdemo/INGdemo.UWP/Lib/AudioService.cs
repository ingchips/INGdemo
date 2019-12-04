using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using INGdemo.Lib;

[assembly: Xamarin.Forms.Dependency(typeof(INGdemo.UWP.Lib.AudioServiceImpl))]
namespace INGdemo.UWP.Lib
{
    class AudioServiceImpl : IPCMAudio
    {
        public bool Write(Int16[] samples)
        {
            return true;
        }

        public void Play()
        {

        }

        public void Stop()
        {

        }
    }
}
