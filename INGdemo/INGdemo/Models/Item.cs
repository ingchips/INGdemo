using System;
using System.Text;
using System.Collections.ObjectModel;
using System.Linq;

using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;

using INGota.FOTA;
using INGota.Models;

namespace INGdemo.Models
{
    public class BLEAdvSimpleInfo
    {
        public string Title { get; set; }
        public string Data { get; set; }
    }

    public class IBeacon
    {
        public string uuid;
        public int major;
        public int minor;
        public int refPower;
        public double distance = 0.0;
        public bool valid = false;

        const int APPLE_COMPANY_ID      = 0x004C;
        const int IBEACON_ID            = 0x1502;

        /*
            typedef struct ibeacon_adv
            {
                uint16_t apple_id;
                uint16_t id;
                uint8_t  uuid[16];
                uint16_t major;
                uint16_t minor;
                int8_t   ref_power;
            } ibeacon_adv_t;
            #define
            #define IBEACON_ID              0x1502
        */

        public bool ParseData(byte []buffer)
        {
            if (buffer.Length != 25)
                return false;
            if (BitConverter.ToUInt16(buffer, 0) != APPLE_COMPANY_ID)
                return false;
            if (BitConverter.ToUInt16(buffer, 2) != IBEACON_ID)
                return false;

            var uuid = buffer.Skip(4).Take(16).ToArray();
            this.uuid = string.Format("{16}{0:X2}{1:X2}{2:X2}{3:X2}-{4:X2}{5:X2}-{6:X2}{7:X2}-" +
                                       "{8:X2}{9:X2}-{10:X2}{11:X2}{12:X2}{13:X2}{14:X2}{15:X2}{17}",
                    uuid[0], uuid[1], uuid[2], uuid[3],
                    uuid[4], uuid[5], uuid[6], uuid[7], uuid[8], uuid[9],
                    uuid[10], uuid[11], uuid[12], uuid[13], uuid[14], uuid[15], "{", "}");
            major = BitConverter.ToUInt16(buffer, 4 + 16);
            minor = BitConverter.ToUInt16(buffer, 4 + 16 + 2);
            refPower = (sbyte)buffer[4 + 16 + 4];

            valid = true;
            return true;
        }

        public void UpdateDistance(int Rssi)
        {
            distance = Math.Pow(10, (refPower - Rssi) / 20.0);
        }
    }

    public class BLEDev
    {
        public IBeacon iBeacon;

        public BLEDev(IDevice ADevice)
        {
            iBeacon = new IBeacon();
            BLEAdvSimpleInfos = new ObservableCollection<BLEAdvSimpleInfo>();
            Device = ADevice;

            if (Device == null)
                return;

            Id = Utils.GetDeviceAddress(Device.Id);
            Name = Device.Name != null ? Device.Name : Id;
            Address = Device.Name != null ? Id : "";
            Connectable = false;

            RSSI = Device.Rssi;

            IconString = "";
            foreach (var x in Device.AdvertisementRecords)
            {
                var info = new BLEAdvSimpleInfo
                {
                    Title = x.Type.ToString(),
                    Data = Utils.DecodeAdvertisement(x)
                };
                BLEAdvSimpleInfos.Add(info);
                if (x.Type == AdvertisementRecordType.ManufacturerSpecificData)
                {
                    iBeacon.ParseData(x.Data);
                    if (iBeacon.valid)
                        iBeacon.UpdateDistance(Device.Rssi);
                }
            }

            Connectable = Utils.IsConnectable(Device.AdvertisementRecords);

            foreach (var service in Utils.GetServices(Device.AdvertisementRecords))
            {
                var s = BLEServices.GetServiceIcon(service);
                if (s != null)
                {
                    IconString = s;
                    break;
                }
            }

            if (iBeacon.valid)
            {
                IconString = "⌖";
                Name = "iBeacon";
                Address = Id;
            }

            if (IconString.Length < 1)
                IconString = " ? ";
        }

        public string Id { get; }
        public string Name { get; }
        public string Address { get; }
        public string IconString { get; }
        public int RSSI { get; }
        public string Info { get
            {
                if (iBeacon.valid)
                {
                    //return iBeacon.uuid + "\n" + String.Format("Major: {0:X4} Minor: {1:X4} Distance: {2:F1}m",
                    //    iBeacon.major, iBeacon.minor, iBeacon.distance);
                    return iBeacon.uuid + "\n" + String.Format("Major: {0:X4} Minor: {1:X4}\nDistance: {2:F1}m",
                          iBeacon.major, iBeacon.minor, iBeacon.distance);
                }
                else
                    return Address;
            }
        }
        public IDevice Device { get; set; }
        public bool Connectable { get; set; }
        public ObservableCollection<BLEAdvSimpleInfo> BLEAdvSimpleInfos { get; set; }
    }
}