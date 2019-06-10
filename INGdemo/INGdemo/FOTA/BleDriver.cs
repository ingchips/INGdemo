using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;
using System.Threading.Tasks;

namespace INGota.FOTA
{
    interface IBleDriver
    {
        Task<byte[]> ReadVer();

        Task<byte[]> ReadCtrl();

        Task<byte[]> ReadData();

        Task<bool> WriteCtrl(byte[] data);
        Task<bool> WriteData(byte[] data);

        Task Init(IDevice device, IEnumerable<IService> services);

        bool Available { get; }
    }

    class BleDriver: IBleDriver
    {
        protected Guid GUID_SERVICE;
        protected Guid GUID_CHAR_OTA_VER;
        protected Guid GUID_CHAR_OTA_CTRL;
        protected Guid GUID_CHAR_OTA_DATA;
        //readonly static Guid GUID_CHAR_OTA_ENTRY    = new Guid("3345c2f4-6f36-45c5-8541-92f56728d5f3");       

        ICharacteristic charVer;
        ICharacteristic charCtrl;
        ICharacteristic charData;

        public BleDriver(Guid GUID_SERVICE,
                         Guid GUID_CHAR_OTA_VER,
                         Guid GUID_CHAR_OTA_CTRL,
                         Guid GUID_CHAR_OTA_DATA)
        {
            this.GUID_SERVICE = GUID_SERVICE;
            this.GUID_CHAR_OTA_VER = GUID_CHAR_OTA_VER;
            this.GUID_CHAR_OTA_CTRL = GUID_CHAR_OTA_CTRL;
            this.GUID_CHAR_OTA_DATA = GUID_CHAR_OTA_DATA;
        }

        public async Task Init(IDevice device, IEnumerable<IService> services)
        {
            IService service = services.FirstOrDefault((s) => s.Id == GUID_SERVICE);
            if (null == service)
                return;
            var chars = await service.GetCharacteristicsAsync();
            charVer  = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_OTA_VER);
            charCtrl = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_OTA_CTRL);
            charData = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_OTA_DATA);
            if (charData != null)
                charData.WriteType = CharacteristicWriteType.WithoutResponse;
        }

        public bool Available { get { return (charVer != null) && (charCtrl != null) && (charData != null); } }

        async Task<byte[]> ReadCharacteristics(ICharacteristic ch)
        {
            if (ch == null)
                return null;
            try
            {
                await ch.ReadAsync();
                return ch.Value;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> WriteCharacteristics(ICharacteristic ch, byte[] data)
        {
            if (ch == null)
                return false;
            try
            {
                return await ch.WriteAsync(data);
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> ReadVer()
        {
            return await ReadCharacteristics(charVer);
        }

        public async Task<byte[]> ReadCtrl()
        {
            return await ReadCharacteristics(charCtrl);
        }

        public async Task<byte[]> ReadData()
        {
            return await ReadCharacteristics(charData);
        }

        public async Task<bool> WriteCtrl(byte[] data)
        {
            return await WriteCharacteristics(charCtrl, data);
        }

        public async Task<bool> WriteData(byte[] data)
        {
            return await WriteCharacteristics(charData, data);
        }
    }
}
