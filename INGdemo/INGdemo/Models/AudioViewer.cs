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

namespace INGdemo.Models
{
    class AudioViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("00000001-494e-4743-4849-505355554944");
        static public Guid GUID_CHAR_CTRL = new Guid("bf83f3f1-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_OUT = new Guid("bf83f3f2-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_INFO = new Guid("10000001-494e-4743-4849-505355554944");
        static public string SERVICE_NAME = "INGChips Voice Output Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F3A4);

        IDevice BleDevice;
        IService service;
        ICharacteristic charCtrl;
        ICharacteristic charOutput;

        Label label;
        Label labelInfo;
        ADPCMDecoder Decoder;
        IPCMAudio Player;
        Slider Gain;
        Label GainInd;
        int CurrentGain = 0;

        public View MakeSlider(string label, out Slider slider)
        {
            var layout = new StackLayout();
            layout.Orientation = StackOrientation.Horizontal;
            layout.HorizontalOptions = LayoutOptions.Fill;

            layout.Children.Add(new Label { Text = label, Style = Device.Styles.TitleStyle });
            slider = new Slider(-5, 5, 0);
            slider.HorizontalOptions = LayoutOptions.FillAndExpand;
            layout.Children.Add(slider);

            GainInd = new Label();
            GainInd.Text = "0dB";
            GainInd.Style = Device.Styles.CaptionStyle;
            GainInd.HorizontalOptions = LayoutOptions.End;
            layout.Children.Add(GainInd);

            return layout;
        }

        void InitUI()
        {
            var layout = new StackLayout();
            label = new Label();

            var label2 = new Label();
            label2.Text = ICON_STR;
            label2.FontSize = 70;
            label2.HorizontalOptions = LayoutOptions.Center;

            labelInfo = new Label();
            labelInfo.Style = Device.Styles.CaptionStyle;
            labelInfo.HorizontalOptions = LayoutOptions.Center;

            label.HorizontalOptions = LayoutOptions.Center;
            label.FontSize = 10;

            layout.Children.Add(label2);
            layout.Children.Add(labelInfo);
            layout.Children.Add(MakeSlider("Gain", out Gain));
            layout.Children.Add(label);

            Gain.ValueChanged += Gain_ValueChanged;

            layout.Margin = 20;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;
            Content = new ScrollView { Content = layout };
            Title = SERVICE_NAME;
        }

        async private void Gain_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            int gain = (int)Math.Round(Gain.Value);
            Gain.Value = gain;
            
            if (CurrentGain != gain)
            {
                CurrentGain = gain;
                GainInd.Text = string.Format("{0}dB", 3 * gain);
                await charCtrl.WriteAsync(new byte[1] { (byte)(gain & 0xff) });
            }
        }

        async void Read()
        {
            charCtrl = await service.GetCharacteristicAsync(GUID_CHAR_CTRL);
            charOutput = await service.GetCharacteristicAsync(GUID_CHAR_OUT);
            var charInfo = await service.GetCharacteristicAsync(GUID_CHAR_INFO);
            var info = await charInfo.ReadAsync();
            int size = info[0];
            labelInfo.Text = string.Format("BlockSize = {0} B", size);
            size = Utils.Att2MTUSize(size);
            var this_size = await BleDevice.RequestMtuAsync(size);
            if (this_size < size)
            {
                var msg = string.Format("Your BLE subsystem can't support required block size ({0} B).", this_size);
                await DisplayAlert("Error", msg, "OK");
                return;
            }
            BleDevice.UpdateConnectionInterval(ConnectionInterval.High);
            charOutput.ValueUpdated += CharOutput_ValueUpdated;
            await charOutput.StartUpdatesAsync();
        }

        private void CharOutput_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            Decoder.Decode(e.Characteristic.Value);
            Device.BeginInvokeOnMainThread(() =>
                label.Text = Utils.ByteArrayToString(e.Characteristic.Value)
            );
        }

        public AudioViewer(IDevice ADevice, IList<IService> services)
        {
            Decoder = new ADPCMDecoder(8000 / 10);
            Player = DependencyService.Get<IPCMAudio>();
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Decoder.PCMOutput += Decoder_PCMOutput;
            Player.Play();
            Read();
        }

        private void Decoder_PCMOutput(object sender, short[] e)
        {
            Player.Write(e);
        }

        async protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Player.Stop();
            if (BleDevice.State == DeviceState.Connected) await charOutput.StopUpdatesAsync();
        }
    }
}
