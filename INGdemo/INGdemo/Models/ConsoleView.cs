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
    class ConsoleViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("43f4b114-ca67-48e8-a46f-9a8ffeb7146a");
        static public Guid GUID_CHAR_IN = new Guid("bf83f3f1-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_OUT = new Guid("bf83f3f2-399a-414d-9035-ce64ceb3ff67");
        static public string SERVICE_NAME = "INGChips Console Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F4BB);

        Editor DataLog;
        Entry Input;
        Button BtnSend;
        IService service;
        IDevice BleDevice;
        ICharacteristic charInput;
        ICharacteristic charOutput;

        public void InitUI()
        {
            var layout = new StackLayout();

            var toolbar2 = new StackLayout();
            toolbar2.Orientation = StackOrientation.Horizontal;
            toolbar2.Margin = 0;
            toolbar2.Spacing = 10;
            toolbar2.VerticalOptions = LayoutOptions.End;

            Input = new Entry
            {
                HorizontalOptions = LayoutOptions.FillAndExpand
            };
            BtnSend = new Button
            {
                Text = "Send",
                HorizontalOptions = LayoutOptions.End
            };
            BtnSend.Clicked += BtnSend_Clicked;
            toolbar2.Children.Add(Input);
            toolbar2.Children.Add(BtnSend);

            DataLog = new Editor
            {                
                FontFamily = Utils.GetMonoFamily(),
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = -1
            };
            
            layout.Children.Add(DataLog);
            layout.Children.Add(toolbar2);

            layout.Margin = 10;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;
            Content = new ScrollView
            {
                Content = layout,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill
            };
            Title = SERVICE_NAME;
        }

        async private void BtnSend_Clicked(object sender, EventArgs e)
        {
            if (Input.Text?.Length < 1) return;
            byte[] bytes = Encoding.UTF8.GetBytes(Input.Text);
            var buf = new byte[bytes.Length + 1];
            Array.Copy(bytes, buf, bytes.Length);
            buf[bytes.Length] = 0;
            await charInput.WriteAsync(buf);
            DataLog.Text = DataLog.Text + "\n> " + Input.Text + "\n";
            Input.Text = "";
        }

        async void Read()
        {
            var chars = await service.GetCharacteristicsAsync();
            charInput = chars.FirstOrDefault((x) => x.Id.Equals(GUID_CHAR_IN));
            charOutput = chars.FirstOrDefault((x) => x.Id.Equals(GUID_CHAR_OUT));
            charOutput.ValueUpdated += CharOutput_ValueUpdated;
            BleDevice.UpdateConnectionInterval(ConnectionInterval.Low);
            await BleDevice.RequestMtuAsync(250);
            await charOutput.StartUpdatesAsync();
        }

        private void CharOutput_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            var b = charOutput.Value;
            if (b == null) return;
            b = b.Select((v) => v != 0 ? v : (byte)10).ToArray();
            Device.BeginInvokeOnMainThread(() =>
            {
                DataLog.Text = DataLog.Text + Encoding.UTF8.GetString(b);
            });
        }

        public ConsoleViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        async protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (BleDevice.State == DeviceState.Connected) await charOutput.StopUpdatesAsync();
        }
    }
}
