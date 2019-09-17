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
    public class WaitActivity : ContentPage
    {
        Label ProgressMsg;
        ProgressBar bar;
        ActivityIndicator activity;

        public WaitActivity()
        {
            activity = new ActivityIndicator();
            var layout = new StackLayout();
            ProgressMsg = new Label();
            bar = new ProgressBar();
            activity.VerticalOptions = LayoutOptions.EndAndExpand;
            ProgressMsg.VerticalOptions = LayoutOptions.StartAndExpand;
            ProgressMsg.HorizontalOptions = LayoutOptions.Center;

            layout.Children.Add(activity);
            //layout.Children.Add(new BoxView { BackgroundColor = Color.Red, HeightRequest = 1 });
            layout.Children.Add(bar);
            //layout.Children.Add(new BoxView { BackgroundColor = Color.Red, HeightRequest = 1 });
            layout.Children.Add(ProgressMsg);

            layout.Margin = 20;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;
            activity.IsRunning = true;
            
            bar.VerticalOptions = LayoutOptions.Start;
            Content = layout;
            
            Title = "Please wait...";
            bar.Progress = 0.0;
            bar.IsVisible = false;
        }

        public string Message { set { ProgressMsg.Text = value; } }
        public double Progress { set { bar.Progress = value; bar.IsVisible = true; activity.IsRunning = false; } }
    }

    class Thermometer : ContentPage
    {
        public const string UUID_SERVICE_THERMO     =         "00001809-0000-1000-8000-00805F9B34FB";
        static public Guid GUID_SERVICE      = new Guid(UUID_SERVICE_THERMO);
        static public Guid GUID_CHAR_DEV_NAME       = new Guid("00002A00-0000-1000-8000-00805F9B34FB");
        static public Guid GUID_CHAR_THERMO_VALUE   = new Guid("00002A1C-0000-1000-8000-00805F9B34FB");
        static public Guid GUID_CHAR_THERMO_TYPE    = new Guid("00002A1D-0000-1000-8000-00805F9B34FB");
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F321);

        IDevice BleDevice;
        ICharacteristic charMeasValue;
        ICharacteristic charMeasType;

        Label uiMeasValue;
        Label uiMeasType;
        Button uiRefresh;

        IService service;
        bool useNotify = false;

        public void InitUI()
        {
            var layout = new StackLayout();
            var label = new Label();
            uiMeasValue = new Label();
            uiMeasType = new Label();
            uiRefresh = new Button();

            uiRefresh.Text = "Refresh";
            uiRefresh.Clicked += Refresh_Clicked;
            uiRefresh.HorizontalOptions = LayoutOptions.Center;

            label.Text = "\n" + Char.ConvertFromUtf32(0x1f321);
            label.FontSize = 50;
            label.HorizontalOptions = LayoutOptions.Center;

            uiMeasValue.Style = Device.Styles.TitleStyle;
            uiMeasValue.HorizontalOptions = LayoutOptions.Center;
            uiMeasType.Style = Device.Styles.CaptionStyle;
            uiMeasType.HorizontalOptions = LayoutOptions.Center;

            layout.Children.Add(label);
            layout.Children.Add(uiMeasValue);
            layout.Children.Add(uiMeasType);
            layout.Children.Add(uiRefresh);

            layout.Margin = 20;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;
            Content = new ScrollView { Content = layout };
            Title = BleDevice.Name;

            layout.Children.Add(new BoxView { HeightRequest = 60 });
        }

        private void ShowMeasValue(byte []charValue)
        {
            if (charValue.Length != 5)
            {
                uiMeasType.Text = "N/A";
                return;
            }
            var t = new byte[4];
            for (var i = 1; i <= 4; i++)
                t[i - 1] = charValue[i];

            uiMeasValue.Text = Utils.float_ieee_11073_val_to_repr((UInt32)Utils.ParseLittleInt(t)) + "°C";
        }

        private async void Refresh_Clicked(object sender, EventArgs e)
        {
            if (charMeasValue == null)
                return;
            uiRefresh.IsEnabled = false;
            await charMeasValue.ReadAsync();
            ShowMeasValue(charMeasValue.Value);
            uiRefresh.IsEnabled = true;
        }

        static string [] measType = new string [] { "n/a",  "Armpit", "Body", "Ear", "Finger", "Gastro-intestinal Tract",
                                                    "Mouth",  "Rectum",  "Toe", "Tympanum"};


        async Task Connect(IReadOnlyList<IService> services)
        {
            var adapter = CrossBluetoothLE.Current.Adapter;
            service = services.FirstOrDefault((s) => s.Id == GUID_SERVICE);
            if (null == service)
                throw new Exception("GUID_SERVICE_THERMO not avaliable.");
            var chars = await service.GetCharacteristicsAsync();
            charMeasValue = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_THERMO_VALUE);
            charMeasType  = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_THERMO_TYPE);

            if ((charMeasValue == null) || (charMeasType == null))
                throw new Exception("some characteristics not avaliable.");

            await charMeasValue.ReadAsync();
            await charMeasType.ReadAsync();

            if ((charMeasValue.Properties & (CharacteristicPropertyType.Notify | CharacteristicPropertyType.Indicate)) > 0)
            {
                charMeasValue.ValueUpdated += CharMeasValue_ValueUpdated;
                await charMeasValue.StartUpdatesAsync();
                useNotify = true;
            }

            uiRefresh.IsVisible = !useNotify;

            Title = "Thermometer Service";
            uiMeasType.Text = measType[charMeasType.Value[0]];
            ShowMeasValue(charMeasValue.Value);
        }

        private void CharMeasValue_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() => {
                ShowMeasValue(charMeasValue.Value);
            });
        }

        public Thermometer(IDevice ADevice, IReadOnlyList<IService> services)
        { 
            BleDevice = ADevice;
            if (BleDevice == null)
                return;

            InitUI();
            ReadData(services);
        }

        async protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (useNotify && (BleDevice.State == DeviceState.Connected))
                await charMeasValue.StopUpdatesAsync();
        }

        async void ReadData(IReadOnlyList<IService> services)
        {
            bool error = false;
            try
            {
                await Connect(services);             
            }
            catch (Exception e)
            {
                await DisplayAlert("Alert", e.Message, "OK");
                error = true;
                return;
            }

            if (error)
            {
                await Navigation.PopAsync();
                return;
            }
        }
    }
}
