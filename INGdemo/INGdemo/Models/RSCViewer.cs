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
    class RSCViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("00001814-0000-1000-8000-00805F9B34FB");
        static public Guid GUID_CHAR_RSC_MEAS = new Guid("00002A53-0000-1000-8000-00805F9B34FB");
        static public string SERVICE_NAME = "Running Speed and Cadence Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F6B6);

        IService service;
        ICharacteristic charMeas;

        Label vStatus;
        Label vSpeed;
        Label vCadence;
        Label vStrideLength;
        Label vTotalDistance;

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

            grid.Children.Add(BuildItem("Speed", out vSpeed), 0, 1);
            grid.Children.Add(BuildItem("Cadence", out vCadence), 1, 1);
            grid.Children.Add(BuildItem("Stride Length", out vStrideLength), 0, 2);
            grid.Children.Add(BuildItem("Total Distance", out vTotalDistance), 1, 2);

            grid.Margin = 20;

            Content = new ScrollView { Content = grid };
            Title = SERVICE_NAME;
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
            if (charMeas.Value?.Length < 10)
                return;

            var flags = charMeas.Value[0];
            var speed = (charMeas.Value[1] | (charMeas.Value[2] << 8)) / 256.0;
            var cadence = charMeas.Value[3];

            vStatus.Text = (flags & 0x4) != 0 ? "RUNNING" : "WALKING";

            vSpeed.FormattedText = showValue(string.Format("{0:F1}", speed), "m/s");
            vCadence.FormattedText = showValue(string.Format("{0}", cadence), "RPM");

            var i = 4;
            if ((flags & 0x1) != 0)
            {
                var stride = (charMeas.Value[i] | (charMeas.Value[i + 1] << 8)) / 100.0;
                i += 2;
                vStrideLength.FormattedText = showValue(string.Format("{0:F2}", stride), "m");
            }

            if ((flags & 0x2) != 0)
            {
                var total = (charMeas.Value[i]
                              | ((uint)charMeas.Value[i + 1] << 8)
                              | ((uint)charMeas.Value[i + 2] << 16)
                              | ((uint)charMeas.Value[i + 3] << 24)) / 10.0;
                vTotalDistance.FormattedText = 
                    total > 1000 ? showValue(string.Format("{0:F1}", total / 1000), "km") 
                                 : showValue(string.Format("{0:F1}", total), "m");
            }
        }

        async void Read()
        {
            if (service == null)
                return;

            var chars = await service.GetCharacteristicsAsync();

            charMeas = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_RSC_MEAS);
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

        public RSCViewer(IDevice ADevice, IReadOnlyList<IService> services)
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
