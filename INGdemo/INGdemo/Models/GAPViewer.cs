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
    class GAPViewer : ContentPage
    {
        public const string UUID_SERVICE_GAP = "00001800-0000-1000-8000-00805F9B34FB";
        static public Guid GUID_SERVICE = new Guid(UUID_SERVICE_GAP);
        static public Guid GUID_CHAR_DEV_NAME = new Guid("00002A00-0000-1000-8000-00805F9B34FB");
        static public Guid GUID_CHAR_DEV_APPEARANCE = new Guid("00002A01-0000-1000-8000-00805F9B34FB");
        static public string SERVICE_NAME = "Generic Access";
        static public string ICON_STR = "";

        Label GapName;
        Label GapAppearance;
        IService service;

        Label[] CreateItem(string title)
        {
            var r = new Label[] { new Label(), new Label() };
            r[0].Text = title;
            r[0].FontSize = Device.GetNamedSize(NamedSize.Large, r[0]);
            return r;
        }

        public void InitUI()
        {
            var stack = new StackLayout();
            stack.Padding = 10;

            var t = CreateItem("Device Name");
            GapName = t[1];
            stack.Children.Add(t[0]); stack.Children.Add(t[1]);

            t = CreateItem("Appearance");
            GapAppearance = t[1];
            stack.Children.Add(t[0]); stack.Children.Add(t[1]);

            Content = stack;
            Title = "GAP";
        }

        async void Read()
        {
            if (service == null)
                return;

            var chars = await service.GetCharacteristicsAsync();

            var achar = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_DEV_NAME);
            if (achar != null)
                GapName.Text = Encoding.UTF8.GetString(await achar.ReadAsync());

            achar = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_DEV_APPEARANCE);
            if (achar != null)
                GapAppearance.Text = getAppearance((int)Utils.ParseLittleInt(await achar.ReadAsync()) >> 6);
        }

        public GAPViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        string getAppearance(int v)
        {
            switch (v)
            {
                case 0: return "Unknown";
                case 1: return "Phone";
                case 2: return "Computer";
                case 3: return "Watch";
                case 4: return "Clock";
                case 5: return "Display";
                case 6: return "Remote Control";
                case 7: return "Eye-glasses";
                case 8: return "Tag";
                case 9: return "Keyring";
                case 10: return "Media Player";
                case 11: return "Barcode Scanner";
                case 12: return "Thermometer";
                case 13: return "Heart rate Sensor";
                case 14: return "Blood Pressure";
                case 15: return "Human Interface Device (HID)";
                case 16: return "Glucose Meter";
                case 17: return "Running Walking Sensor";
                case 18: return "Cycling";
                case 49: return "Pulse Oximeter";
                case 50: return "Weight Scale";
                case 51: return "Personal Mobility Device";
                case 52: return "Continuous Glucose Monitor";
                case 53: return "Insulin Pump";
                case 54: return "Medication Delivery";
                case 81: return "Outdoor Sports Activity";
                default: return string.Format("unknown {0}", v);
            }
        }

    }
}
