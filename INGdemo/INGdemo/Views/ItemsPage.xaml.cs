using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using INGota.Models;
using INGota.Views;
using INGota.ViewModels;

using INGdemo.Models;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;
using Xamarin.Essentials;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace INGota.Views
{
    class DevUI
    {
        public BLEDev dev;
        Label rssi;
        Label iconStr;
        Label name;
        Label info;
        Label interval;
        double interval_value = -1;
        DateTime lastSeen;
        Button btn;

        public StackLayout ui;
        StackLayout extra;

        void ShowExtra()
        {
            foreach (var x in dev.BLEAdvSimpleInfos)
            {
                var container = new StackLayout();
                container.Padding = 0;
                container.Orientation = StackOrientation.Horizontal;
                container.HorizontalOptions = LayoutOptions.FillAndExpand;
                var label = new Label();
                label.FontSize = 10;
                label.FontAttributes = FontAttributes.Bold;
                label.Text = x.Title + ": ";
                container.Children.Add(label);
                label = new Label();
                label.FontSize = 10;
                label.Text = x.Data;
                container.Children.Add(label);
                extra.Children.Add(container);
            }
        }

        public void UpdateUI(IDevice ADevice, bool init = false)
        {
            var now = DateTime.Now;

            if (init)
            {
                lastSeen = now;
            }
            else
            {
                var delta = now - lastSeen;
                lastSeen = now;
                var intv = delta.TotalMilliseconds;
                if (intv < 20) intv = 20;
                if ((interval_value < 0.0) || (intv < interval_value))
                {
                    interval_value = intv;
                    interval.Text = string.Format("{0:f0}", interval_value);
                }
            }
            dev = new BLEDev(ADevice);
            rssi.Text = string.Format("{0} dBm", dev.RSSI);            
            name.Text = dev.Name;
            info.Text = dev.Info;
            btn.CommandParameter = ADevice.Id.ToString();
            btn.IsVisible = dev.Connectable;
        }

        public DevUI(IDevice ADevice, EventHandler handle)
        {
            dev = new BLEDev(ADevice);

            ui = new StackLayout();
            ui.Orientation = StackOrientation.Horizontal;

            var sig = new StackLayout();
            sig.Orientation = StackOrientation.Vertical;
            sig.Padding = 0;
            sig.WidthRequest = 40;

            var cont2 = new StackLayout();
            cont2.Orientation = StackOrientation.Vertical;
            cont2.HorizontalOptions = LayoutOptions.FillAndExpand;

            iconStr = new Label();
            iconStr.Style = Device.Styles.ListItemTextStyle;
            iconStr.FontSize = 30;
            iconStr.VerticalTextAlignment = TextAlignment.Center;            
            iconStr.HorizontalTextAlignment = TextAlignment.Center;
            iconStr.Text = dev.IconString;
            if (iconStr.Text.IndexOf("?") >= 0) iconStr.TextColor = Color.Orange;

            sig.Children.Add(iconStr); 

            var titlebar = new StackLayout();
            titlebar.Orientation = StackOrientation.Horizontal;

            var container = new StackLayout();
            container.Padding = 2;
            container.Orientation = StackOrientation.Vertical;
            container.HorizontalOptions = LayoutOptions.FillAndExpand;
            name = new Label();
            name.Style = Device.Styles.ListItemTextStyle;
            name.FontSize = 16;
            info = new Label();
            info.Style = Device.Styles.ListItemDetailTextStyle;
            name.FontSize = 13;

            btn = new Button();
            btn.Text = "Connect";
            btn.Clicked += handle;

            container.Children.Add(name);
            container.Children.Add(info);

            titlebar.Children.Add(container);
            titlebar.Children.Add(btn);

            extra = new StackLayout();
            extra.Orientation = StackOrientation.Vertical;

            var sig_cont = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Margin = 8
            };

            interval = new Label();
            interval.Style = Device.Styles.ListItemDetailTextStyle;

            rssi = new Label();
            rssi.Style = Device.Styles.ListItemDetailTextStyle;

            sig_cont.Children.Add(new Label
            {
                Style = Device.Styles.ListItemTextStyle,
                //FontSize = 15,
                TextColor = Color.Blue,
                Text = "📶 "
            });
            sig_cont.Children.Add(rssi);
            sig_cont.Children.Add(new Label
            {
                Style = Device.Styles.ListItemTextStyle,
                //FontSize = 15,
                TextColor = Color.Orange,
                Text = "   ⬌ "
            });
            sig_cont.Children.Add(interval);

            cont2.Children.Add(titlebar);
            cont2.Children.Add(extra);
            cont2.Children.Add(sig_cont);

            cont2.Children.Add(new BoxView
            {
                HeightRequest = 1,
                Color = Color.Gray
            });

            ui.Children.Add(sig);
            ui.Children.Add(cont2);

            ShowExtra();

            UpdateUI(ADevice, true);
        }
    }

    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ItemsPage : ContentPage
    {
        IDevice BleDevice;
        bool disc = true;
        Dictionary<Guid, DevUI> devicesUI = new Dictionary<Guid, DevUI>();

        public ItemsPage()
        {
            InitializeComponent();

            var adapter = CrossBluetoothLE.Current.Adapter;
            adapter.DeviceConnectionLost += Adapter_DeviceConnectionLost;
            adapter.DeviceDisconnected += Adapter_DeviceDisconnected;

            RSSIThres.ValueChanged += RSSIThres_ValueChanged;
            NameFilter.TextChanged += NameFilter_TextChanged;
            NameNonEmpty.Toggled += NameNonEmpty_Toggled;
        }

        private void NameNonEmpty_Toggled(object sender, ToggledEventArgs e)
        {
            ApplyFilterToAll();
        }

        private void NameFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilterToAll();
        }

        private void RSSIThres_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            RSSIThresLabel.Text = string.Format("{0:f0}", RSSIThres.Value);
            ApplyFilterToAll();
        }

        private void DeviceDisconnected()
        {
            BleDevice = null;
            Device.BeginInvokeOnMainThread(async () => {
                if (Navigation.NavigationStack.Last() != this)
                {
                    if (!disc)
                        await DisplayAlert(
                                "Disconnected",
                                "Device disconnected!", "OK");
                    await Navigation.PopToRootAsync();
                }
            });
        }

        private void Adapter_DeviceDisconnected(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            DeviceDisconnected();
        }

        private void Adapter_DeviceConnectionLost(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceErrorEventArgs e)
        {
            DeviceDisconnected();
        }

        public async void Scan_Clicked(object sender, EventArgs e)
        {
            await ExecuteScan();
        }

        void ApplyFilterToAll()
        {
            foreach (var pair in devicesUI)
                ApplyFilter(pair.Value);
        }

        void ApplyFilter(DevUI ui)
        {
            bool vis = false;
            do
            {
                if (ui.dev.RSSI < RSSIThres.Value) break;
                if (NameNonEmpty.IsToggled)
                {
                    if (!ui.dev.HasAdvName) break;
                }
                else if (NameFilter.Text?.Length > 0)
                {
                    CompareInfo sampleCInfo = CultureInfo.InvariantCulture.CompareInfo;

                    int index = sampleCInfo.IndexOf(
                         ui.dev.Name, NameFilter.Text, CompareOptions.IgnoreCase);

                    if (index < 0) break;
                }
                
                vis = true;
            } while (false);

            ui.ui.IsVisible = vis;
        }

        public async Task ExecuteScan()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                DevListView.Children.Clear();
                devicesUI.Clear();

                var adapter = CrossBluetoothLE.Current.Adapter;
                adapter.ScanTimeout = 0x7fffffff;

                switch (Device.RuntimePlatform)
                {
                    case Device.UWP:
                        adapter.ScanMode = Plugin.BLE.Abstractions.Contracts.ScanMode.Passive;
                        break;
                }

                adapter.DeviceAdvertised += (s, a) =>
                {
                    Device.BeginInvokeOnMainThread(async () => {
                        if (devicesUI.ContainsKey(a.Device.Id))
                        {
                            devicesUI[a.Device.Id].UpdateUI(a.Device);
                        }
                        else
                        {
                            var ui = new DevUI(a.Device, Connect_Clicked);
                            devicesUI.Add(a.Device.Id, ui);
                            ApplyFilter(ui);
                            DevListView.Children.Add(ui.ui);
                        }
                    });
                    
                };
                await adapter.StartScanningForDevicesAsync();
            }
            catch (Exception ex)
            {
            }
            finally
            {
                IsBusy = false;
            }
        }

        async void Connect_Clicked(object sender, EventArgs e)
        {
            var Id = Guid.Parse((sender as Button).CommandParameter as string);
            var adapter = CrossBluetoothLE.Current.Adapter;

            if (IsBusy)
            {
                await adapter.StopScanningForDevicesAsync();
                IsBusy = false;
            }

            if (!devicesUI.ContainsKey(Id)) return;

            var dev = devicesUI[Id];
            disc = false;
            BleDevice = dev.dev.Device;
            await Navigation.PushAsync(new BLEServices(BleDevice));
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            var adapter = CrossBluetoothLE.Current.Adapter;
            if ((BleDevice != null) && (BleDevice.State == DeviceState.Connected))
            {
                disc = true;
                try
                {
                    adapter.DisconnectDeviceAsync(BleDevice);
                }
                catch (Exception)
                {
                }
                
                BleDevice = null;
            }
        }
    }
}