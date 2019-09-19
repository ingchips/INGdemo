using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;

using INGota.FOTA;
namespace INGdemo.Models
{
    class DeviceInfoViewer : ContentPage
    {
        public const string UUID_SERVICE_GAP = "0000180A-0000-1000-8000-00805F9B34FB";
        static public Guid GUID_SERVICE = new Guid(UUID_SERVICE_GAP);
        static public string SERVICE_NAME = "Device Information";
        static public string ICON_STR = Char.ConvertFromUtf32(0x2139);
        IService service;

        StackLayout stack;

        async Task<byte []> ReadItem(IEnumerable<ICharacteristic> chars, Guid guid)
        {
            var c = chars.FirstOrDefault((x) => x.Id.Equals(guid));
            if (c == null)
                return new byte[] { };
            return await c.ReadAsync();
        }

        async Task AppendItem(IEnumerable<ICharacteristic> chars, string title, Guid guid)
        {
            var b = await ReadItem(chars, guid);
            if (b?.Length < 1) return;
            AppendItem(title, Encoding.UTF8.GetString(b));
        }

        async Task AppendRawItem(IEnumerable<ICharacteristic> chars, string title, Guid guid)
        {
            var b = await ReadItem(chars, guid);
            if (b?.Length < 1) return;
            AppendItem(title, Utils.ByteArrayToString(b));
        }

        void AppendItem(string title, string value)
        {
            var t = new Label();
            t.Text = title;
            t.FontSize = Device.GetNamedSize(NamedSize.Large, t);
            stack.Children.Add(t);
            stack.Children.Add(new Label { Text = value });
        }

        public void InitUI()
        {
            stack = new StackLayout();
            stack.Padding = 10;

            Content = new ScrollView { Content = stack };
            Title = "Device Information";
        }

        async void Read()
        {
            if (service == null)
                return;

            var chars = await service.GetCharacteristicsAsync();

            await AppendItem(chars, "Manufacturer Name", new Guid("00002A29-0000-1000-8000-00805F9B34FB"));
            await AppendItem(chars, "Model Number", new Guid("00002A24-0000-1000-8000-00805F9B34FB"));
            await AppendItem(chars, "Serial Number", new Guid("00002A25-0000-1000-8000-00805F9B34FB"));
            await AppendItem(chars, "Hardware Revision", new Guid("00002A27-0000-1000-8000-00805F9B34FB"));
            await AppendItem(chars, "Firmware Revision", new Guid("00002A26-0000-1000-8000-00805F9B34FB"));
            await AppendItem(chars, "Software Revision", new Guid("00002A28-0000-1000-8000-00805F9B34FB"));
            await AppendRawItem(chars, "System ID", new Guid("00002A23-0000-1000-8000-00805F9B34FB"));
            await AppendRawItem(chars, "IEEE 11073-20601 Regulatory Certification Data", new Guid("00002A2A-0000-1000-8000-00805F9B34FB"));
            await AppendRawItem(chars, "PnP ID", new Guid("00002A50-0000-1000-8000-00805F9B34FB"));
        }

        public DeviceInfoViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }
    }
}
