using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;

using INGota.FOTA;

using INGdemo.Lib;
using INGota;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OxyPlot;
using System.Web;

using System.Security.Cryptography;
using System.Net;
using System.IO;
using INGdemo.Helpers;

namespace INGdemo.Models
{
    class MusicPlayerViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("00000008-494e-4743-4849-505355554944");
        static public Guid GUID_CHAR_AUDIO = new Guid("bf83f3f1-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_INFO = new Guid("10000001-494e-4743-4849-505355554944");
        static public string SERVICE_NAME = "INGChips Music Player Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F3B5);

        IDevice BleDevice;
        IService service;
        ICharacteristic charAudio;
        ICharacteristic charInfo;

        public void InitUI()
        {
            var stack = new StackLayout();

            Content = new ScrollView { Content = stack };
            Title = SERVICE_NAME;
        }

        async void Read()
        {
            charAudio = await service.GetCharacteristicAsync(GUID_CHAR_AUDIO);
            charInfo = await service.GetCharacteristicAsync(GUID_CHAR_INFO);

            await BleDevice.RequestMtuAsync(250);
        }

        public MusicPlayerViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            BleDevice = ADevice;
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
            InitUI();
        }
    }
}
