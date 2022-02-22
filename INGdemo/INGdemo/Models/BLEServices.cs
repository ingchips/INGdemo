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
using System.Reflection;

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
        IReadOnlyList<IService> services;

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

        static Type[] viewers = new Type[]
            {
                typeof(GAPViewer),
                typeof(DeviceInfoViewer),
                typeof(Thermometer),
                typeof(BatteryViewer),
                typeof(RSCViewer),

                typeof(HeartRateViewer),
                typeof(ThermoFOTAViewer),
                typeof(ETagViewer),
                typeof(LEDViewer),

                typeof(ThroughputViewer),
                typeof(ConsoleViewer),
                typeof(AudioViewer),
                typeof(PianoViewer),
                typeof(SmartHomeViewer),
                typeof(MusicPlayerViewer),
            };
        static private Type FindViewer(Guid guid)
        {
            return viewers.FirstOrDefault((t) => 
                        guid.Equals((Guid)t.GetField("GUID_SERVICE", BindingFlags.Static | BindingFlags.Public)
                                           .GetValue(null)));
        }

        static public string GetServiceIcon(Guid guid)
        {
            Type x = viewers.FirstOrDefault((t) =>
                        guid.Equals((Guid)t.GetField("GUID_SERVICE", BindingFlags.Static | BindingFlags.Public)
                                           .GetValue(null)));
            if (x != null)
            {
                return (string)x.GetField("ICON_STR", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            }
            else
                return null;
        }

        private void ListView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            var item = e.Item as ServiceItem;
            if (item == null)
                return;
            var guid = new Guid(item.UUID);
            Page p;
            Type t = FindViewer(guid);
            if (t != null)
            {
                p = (Page)t.InvokeMember(t.Name, BindingFlags.Public |
                        BindingFlags.Instance | BindingFlags.CreateInstance,
                        null, null, new object[] { BleDevice, services });
                
            }
            else
            {
                p = new GenericServiceView(BleDevice, services.First((s) => s.Id == guid));
            }

            Navigation.PushAsync(p);
        }

        static public string IconOfService(Guid guid)
        {
            FieldInfo t = FindViewer(guid)?.GetField("ICON_STR", BindingFlags.Static | BindingFlags.Public);
            return t != null ? t.GetValue(null).ToString() : "";
        }

        public void showServices(IReadOnlyList<IService> services)
        {
            serviceList.Clear();
            foreach (var s in services)
            {
                var name = s.Name;

                FieldInfo t = FindViewer(s.Id)?.GetField("SERVICE_NAME", BindingFlags.Static | BindingFlags.Public);
                if (t != null)
                    name = t.GetValue(null).ToString();

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
                switch (Device.RuntimePlatform)
                {
                    case Device.Android:
                        await BleDevice.RequestMtuAsync(200);
                        break;
                    default:
                        break;
                }
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
