using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

using Xamarin.Forms;
using Xamarin.Essentials;

using INGdemo.Models;
using INGota.Views;

using Plugin.BLE;

namespace INGota.ViewModels
{
    public class ItemsViewModel : BaseViewModel
    {
        public ObservableCollection<BLEDev> Items { get; set; }
        public Command ScanCommand { get; set; }
        public Page View;

        public event EventHandler ItemsChangedEvent;

        public ItemsViewModel()
        {
            Title = "BLE Devices";
            Items = new ObservableCollection<BLEDev>();
            ScanCommand = new Command(async () => await ExecuteScan());

            MessagingCenter.Subscribe<ItemsViewModel, BLEDev>(this, "ScanResult", async (obj, item) =>
            {
                var newItem = item as BLEDev;
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await updateDev(newItem);
                });
            });
        }


        public async Task ExecuteScan()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                Items.Clear();
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
                    var Item = new BLEDev(a.Device);
                    MessagingCenter.Send(this, "ScanResult", Item);
                };
                await adapter.StartScanningForDevicesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        async private Task updateDev(BLEDev dev)
        {
            var old = Items.FirstOrDefault((x) => x.Id == dev.Id);

            if (old == null)
            {
                switch (Device.RuntimePlatform)
                {
                case Device.iOS:
                    break;
                default:
                    if (dev.iBeacon.valid)
                    {
                        if (dev.iBeacon.uuid == "{494E4743-4849-5053-4F46-46494345424A}")
                        {
                            await View.DisplayAlert("INGCHIPS", "Welcome to INGChips' Beijing Office!", 
                                "Got It");
                        }
                    }
                    break;
                }

                Items.Add(dev);
            }
            else
            {
                
                if (dev.BLEAdvSimpleInfos.Count < 1) return;
                var i = Items.IndexOf(old);
                Items[i] = dev;
                //Items.RemoveAt(i);
               // Items.Insert(i, dev);
            }
        }

        public BLEDev GetDevice(string Id)
        {
            return Items.FirstOrDefault((x) => x.Id == Id);
        }
    }
}