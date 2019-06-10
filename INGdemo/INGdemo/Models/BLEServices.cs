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

using System.Collections.ObjectModel;

using INGota.Models;
using INGota.FOTA;

namespace INGdemo.Models
{
    class ServiceItem
    {
        public string Name { get; set; }
        public string UUID { get; set; }
        public string Icon { get; set; }
    }

    class BLEServices : ContentPage
    {
        IDevice BleDevice;
        WaitActivity Wait;

        StackLayout layout;
        ListView listView;
        ObservableCollection<ServiceItem> serviceList = new ObservableCollection<ServiceItem>();
        IList<IService> services;

        public void InitUI()
        {
            layout = new StackLayout();

            //layout.Margin = 10;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;

            listView = new ListView
            {
                ItemsSource = serviceList,
                ItemTemplate = new DataTemplate(() =>
                {
                    var Name = new Label();
                    Name.SetBinding(Label.TextProperty, new Binding("Name"));
                    Name.FontSize = Device.GetNamedSize(NamedSize.Medium, Name);
                    var UUID = new Label() { };
                    UUID.SetBinding(Label.TextProperty, new Binding("UUID"));

                    var stack = new StackLayout();
                    stack.Padding = 10;
                    stack.Children.Add(Name);
                    stack.Children.Add(UUID);

                    var Icon = new Label();
                    Icon.SetBinding(Label.TextProperty, new Binding("Icon"));
                    Icon.FontSize = 30;
                    Icon.VerticalTextAlignment = TextAlignment.Center;

                    var row = new StackLayout();
                    row.Padding = 10;
                    row.Orientation = StackOrientation.Horizontal;
                    row.Children.Add(Icon);
                    row.Children.Add(stack);

                    //======== make view Cell =========
                    return new ViewCell()
                    {
                        View = row
                    };
                })
            };
            listView.VerticalOptions = LayoutOptions.FillAndExpand;
            listView.HasUnevenRows = true;
            layout.Children.Add(listView);

            listView.ItemTapped += ListView_ItemTapped;

            Content = new ScrollView { Content = layout };
            Title = (BleDevice.Name?.Length > 0) ? "Services of " + BleDevice.Name : "Services";
        }

        private void ListView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            var item = e.Item as ServiceItem;
            if (item == null)
                return;
            var guid = new Guid(item.UUID);

            if (guid.Equals(GAPViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new GAPViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(DeviceInfoViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new DeviceInfoViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(BatteryViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new BatteryViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(Thermometer.GUID_SERVICE))
            {
                Navigation.PushAsync(new Thermometer(BleDevice, services));
                return;
            }

            if (guid.Equals(ThermoFOTAViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new ThermoFOTAViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(RSCViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new RSCViewer(BleDevice, services));
                return;
            }
            if (guid.Equals(HeartRateViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new HeartRateViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(ETagViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new ETagViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(LEDViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new LEDViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(ThroughputViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new ThroughputViewer(BleDevice, services));
                return;
            }

            if (guid.Equals(ConsoleViewer.GUID_SERVICE))
            {
                Navigation.PushAsync(new ConsoleViewer(BleDevice, services));
                return;
            }
        }

        static public string IconOfService(Guid guid)
        {
            if (guid.Equals(DeviceInfoViewer.GUID_SERVICE))
                return DeviceInfoViewer.ICON_STR;

            if (guid.Equals(Thermometer.GUID_SERVICE))
                return Thermometer.ICON_STR;
            if (guid.Equals(BatteryViewer.GUID_SERVICE))
                return BatteryViewer.ICON_STR;
            if (guid.Equals(RSCViewer.GUID_SERVICE))
                return RSCViewer.ICON_STR;
            if (guid.Equals(HeartRateViewer.GUID_SERVICE))
                return HeartRateViewer.ICON_STR;

            if (guid.Equals(ThermoFOTAViewer.GUID_SERVICE))
                return ThermoFOTAViewer.ICON_STR;
            if (guid.Equals(ETagViewer.GUID_SERVICE))
                return ETagViewer.ICON_STR;
            if (guid.Equals(LEDViewer.GUID_SERVICE))
                return LEDViewer.ICON_STR;

            if (guid.Equals(ThroughputViewer.GUID_SERVICE))
                return ThroughputViewer.ICON_STR;
            if (guid.Equals(ConsoleViewer.GUID_SERVICE))
                return ConsoleViewer.ICON_STR;

            return "";
        }

        public void showServices(IList<IService> services)
        {
            serviceList.Clear();
            foreach (var s in services)
            {
                var name = s.Name;
                if (s.Id.Equals(ThermoFOTAViewer.GUID_SERVICE))
                    name = ThermoFOTAViewer.SERVICE_NAME;
                if (s.Id.Equals(ETagViewer.GUID_SERVICE))
                    name = ETagViewer.SERVICE_NAME;
                if (s.Id.Equals(LEDViewer.GUID_SERVICE))
                    name = LEDViewer.SERVICE_NAME;
                if (s.Id.Equals(ThroughputViewer.GUID_SERVICE))
                    name = ThroughputViewer.SERVICE_NAME;
                if (s.Id.Equals(ConsoleViewer.GUID_SERVICE))
                    name = ConsoleViewer.SERVICE_NAME;
                serviceList.Add(new ServiceItem
                {
                    UUID = s.Id.ToString(),
                    Name = name,
                    Icon = IconOfService(s.Id)
                });
            }
        }

        async Task Connect()
        {
            var adapter = CrossBluetoothLE.Current.Adapter;
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            Wait.Message = "stoping...";
            await adapter.StopScanningForDevicesAsync();

            foreach (var d in adapter.ConnectedDevices)
                await adapter.DisconnectDeviceAsync(d);
            Wait.Message = "connecting...";
            try
            {
                source.CancelAfter(5000);
                await adapter.ConnectToDeviceAsync(BleDevice, cancellationToken: token);
                Wait.Message = "discovering...";
                services = await BleDevice.GetServicesAsync();

                showServices(services);
            }
            catch (Exception)
            {
            }
        }

        public BLEServices(IDevice ADevice)
        {
            BleDevice = ADevice;
            if (BleDevice == null)
                return;

            InitUI();
        }

        bool run = false;

        async void ReadData()
        {
            bool error = false;
            try
            {
                Wait = new WaitActivity();
                await Navigation.PushModalAsync(Wait);
                await Connect();
            }
            catch (Exception e)
            {
                await DisplayAlert("Alert", e.Message, "OK");
                error = true;
                return;
            }
            finally
            {
                await Navigation.PopModalAsync();
            }

            if (error)
            {
                await Navigation.PopAsync();
                return;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (run)
                return;

            run = true;

            ReadData();
        }
    }
}
