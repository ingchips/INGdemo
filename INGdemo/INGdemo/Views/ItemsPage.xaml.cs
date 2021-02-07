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

namespace INGota.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ItemsPage : ContentPage
    {
        ItemsViewModel viewModel;
        IDevice BleDevice;
        bool disc = true;

        public ItemsPage()
        {
            InitializeComponent();

            BindingContext = viewModel = new ItemsViewModel();
            viewModel.View = this;

            var adapter = CrossBluetoothLE.Current.Adapter;
            adapter.DeviceConnectionLost += Adapter_DeviceConnectionLost;
            adapter.DeviceDisconnected += Adapter_DeviceDisconnected;

            viewModel.ItemsChangedEvent += ViewModel_ItemsChangedEvent;
        }

        private void ViewModel_ItemsChangedEvent(object sender, EventArgs e)
        {
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

        async void OnItemSelected(object sender, SelectedItemChangedEventArgs args)
        {
            var item = args.SelectedItem as BLEDev;
            if (item == null)
                return;

            await Navigation.PushAsync(new ItemDetailPage(new ItemDetailViewModel(item)));

            // Manually deselect item.
            ItemsListView.SelectedItem = null;
        }

        async void Scan_Clicked(object sender, EventArgs e)
        {
            await viewModel.ExecuteScan();
        }

        async void Connect_Clicked(object sender, EventArgs e)
        {
            var Id = (sender as Button).CommandParameter as string;
            var dev = viewModel.GetDevice(Id);
            if (dev != null)
            {
                disc = false;
                BleDevice = dev.Device;
                await Navigation.PushAsync(new BLEServices(dev.Device));
            }
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