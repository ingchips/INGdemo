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
    class HeartRateViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("0000180D-0000-1000-8000-00805F9B34FB");
        static public Guid GUID_CHAR_MEAS = new Guid("00002A37-0000-1000-8000-00805F9B34FB");
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F493);

        IService service;
        ICharacteristic charMeas;

        Label vStatus;
        Label vHeartRate;
        Label vEnergy;
        Label vRRIntervals;

        IDevice BleDevice;

        View BuildItem(string caption, out Label label)
        {
            var stack = new StackLayout();
            stack.Children.Add(new Label
            {
                Style = Device.Styles.TitleStyle,
                HorizontalTextAlignment = TextAlignment.Center,
                Text = caption
            });
            label = new Label
            {
                Style = Device.Styles.BodyStyle,
                HorizontalTextAlignment = TextAlignment.Center
            };
            stack.Children.Add(label);
            return stack;
        }

        public void InitUI()
        {
            var grid = new Grid
            {
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };

            vStatus = new Label
            {
                Text = "...",
                FontSize = 30,
                HorizontalOptions = LayoutOptions.Center
            };
            grid.Children.Add(vStatus, 0, 2, 0, 1);

            grid.Children.Add(BuildItem("Heart Rate", out vHeartRate), 0, 1);
            grid.Children.Add(BuildItem("Energy Expended", out vEnergy), 1, 1);
            grid.Children.Add(BuildItem("RR Intervals", out vRRIntervals), 0, 2, 2, 3);

            grid.Margin = 20;

            Content = new ScrollView { Content = grid };
            Title = "Heart Rate Service";
        }

        FormattedString showValue(string value, string unit)
        {
            var r = new FormattedString();
            r.Spans.Add(new Span
            {
                Text = value,
                FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(Label)),
                FontAttributes = FontAttributes.Bold,
                ForegroundColor = Color.Blue
            });
            r.Spans.Add(new Span
            {
                Text = " " + unit,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                ForegroundColor = Color.Gray
            });
            return r;
        }

        void showMeas()
        {
            if (charMeas == null)
                return;
            if (charMeas.Value?.Length < 2)
                return;
            int off = 2;
            var flags = charMeas.Value[0];
            uint rate = 0;
            if ((flags & 0x1) == 0)
                rate = charMeas.Value[1];
            else
            {
                rate = charMeas.Value[1] | ((uint)charMeas.Value[2] << 8);
                off = 3;
            }
            vHeartRate.FormattedText = showValue(string.Format("{0}", rate), "bpm");

            switch ((flags >> 1) & 0x3)
            {
                case 2:
                    vStatus.Text = "Not Contacted";
                    break;
                case 3:
                    vStatus.Text = "Contacted";
                    break;
                default:
                    vStatus.Text = "...";
                    break;
            }

            vEnergy.Text = "";
            vRRIntervals.Text = "";

            if ((flags & 0x8) != 0)
            {
                var v = charMeas.Value[off] | ((uint)charMeas.Value[off] << 8);
                vHeartRate.FormattedText = showValue(string.Format("{0}", v), "bpm");
                off += 2;
            }
                
            if ((flags & 0x10) != 0)
            {
                var a = new byte[charMeas.Value.Length - off];
                var l = new List<uint>();
                while (off + 1 < charMeas.Value.Length)
                {
                    l.Add(charMeas.Value[off] | ((uint)charMeas.Value[off + 1] << 8));
                    off += 2;
                }
                vRRIntervals.Text = string.Join(", ", l.Select((x) => x.ToString()));
            }
        }

        async void Read()
        {
            if (service == null)
                return;

            var chars = await service.GetCharacteristicsAsync();

            charMeas = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_MEAS);
            if (charMeas == null)
                return;

            charMeas.ValueUpdated += CharMeas_ValueUpdated;
            await charMeas.StartUpdatesAsync();
        }

        private void CharMeas_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() => {
                showMeas();
            });
        }

        public HeartRateViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        async protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (BleDevice.State == DeviceState.Connected) await charMeas.StopUpdatesAsync();
        }
    }
}
