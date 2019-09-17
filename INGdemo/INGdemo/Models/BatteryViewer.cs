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

namespace INGdemo.Models
{
    class BatteryViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("0000180F-0000-1000-8000-00805F9B34FB");
        static public Guid GUID_CHAR_BATTERY_LEVEL = new Guid("00002A19-0000-1000-8000-00805F9B34FB");
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F50B);

        Label BatteyLevel;
        Button BtnRefresh;
        IService service;
        ICharacteristic charLevel;
        bool useNotify = false;

        public void InitUI()
        {
            var layout = new StackLayout();
            var label = new Label();
            BatteyLevel = new Label();
            BtnRefresh = new Button();

            BtnRefresh.Text = "Refresh";
            BtnRefresh.Clicked += BtnRefresh_Clicked;
            BtnRefresh.HorizontalOptions = LayoutOptions.Center;

            label.Text = "\n" + Char.ConvertFromUtf32(0x1F50B);
            label.FontSize = 70;
            label.HorizontalOptions = LayoutOptions.Center;

            BatteyLevel.Style = Device.Styles.TitleStyle;
            BatteyLevel.HorizontalOptions = LayoutOptions.Center;

            layout.Children.Add(label);
            layout.Children.Add(BatteyLevel);
            layout.Children.Add(BtnRefresh);

            layout.Margin = 20;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;
            Content = new ScrollView { Content = layout };
            Title = "Battery Service";
        }

        void showLevel()
        {
            if (charLevel == null)
                return;
            if (charLevel.Value?.Length > 0)
                BatteyLevel.Text = string.Format("{0}%", charLevel.Value[0]);
        }

        async void Read()
        {
            if (service == null)
                return;

            var chars = await service.GetCharacteristicsAsync();

            charLevel = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_BATTERY_LEVEL);
            if (charLevel == null)
                return;

            try
            {
                await charLevel.ReadAsync();
                showLevel();

                if ((charLevel.Properties & (CharacteristicPropertyType.Notify | CharacteristicPropertyType.Indicate)) > 0)
                {
                    useNotify = true;
                    charLevel.ValueUpdated += CharLevel_ValueUpdated;
                    await charLevel.StartUpdatesAsync();
                }

                BtnRefresh.IsVisible = !useNotify;
            }
            catch (Exception)
            { }
        }

        private void CharLevel_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                showLevel();
                BtnRefresh.IsEnabled = true;
            });
        }

        async private void BtnRefresh_Clicked(object sender, EventArgs e)
        {
            if (charLevel == null)
                return;
            try
            {
                BtnRefresh.IsEnabled = false;
                await charLevel.ReadAsync();
                showLevel();
                BtnRefresh.IsEnabled = true;
            }
            catch (Exception)
            { }
        }

        IDevice BleDevice;

        public BatteryViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        async protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (useNotify && (BleDevice.State == DeviceState.Connected))
                await charLevel.StopUpdatesAsync();
        }
    }
}