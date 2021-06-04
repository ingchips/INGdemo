using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using OxyPlot;

namespace INGota.FOTA
{
    internal class OTABin
    {
        internal int WriteAddress = 0;
        internal int LoadAddress = 0;
        internal byte[] Data;
        internal string Name;
    }

    internal class Ing91800
    {
        internal const int FLASH_BASE   = 0x4000;
        internal const int FLASH_SIZE   = 512 * 1024;
        internal const int FLASH_PAGE_SIZE = 8 * 1024;
    }

    internal class Version
    {
        public int[] app = new int[3];
        public int[] platform = new int[3];
        public string package;

        public override string ToString()
        {
            return String.Format("app: {0}.{1}.{2} | platform : {3}.{4}.{5}",
                                app[0], app[1], app[2],
                                platform[0], platform[1], platform[2]);
        }
    }

    class OTA
    {
        int OTA_BLOCK_SIZE = 20;

        public enum UpdateStatus
        {
            Running,
            Done,
            Error
        }

        public class ProgressArgs
        {
            public double Progress = 0.0;
            public string Msg;
            public UpdateStatus Status;
        };

        public enum OTAStatus
        {
            Checking,
            UpToDate,
            UpdateAvailable,
            ServerError
        }

        const int FLASH_BASE = Ing91800.FLASH_BASE;
        const int FLASH_SIZE = Ing91800.FLASH_SIZE;
        const int FLASH_PAGE_SIZE = Ing91800.FLASH_PAGE_SIZE;
        const int OTA_UPDATE_FLAG = 0x5A5A5A5A;
        const int OTA_LOCK_FLAG = 0x5A5A5A5A;
        const int FLASH_OTA_DATA_HIGH = Ing91800.FLASH_BASE + Ing91800.FLASH_SIZE;

        const int OTA_CTRL_STATUS_DISABLED = 0;
        const int OTA_CTRL_STATUS_OK = 1;
        const int OTA_CTRL_STATUS_ERROR = 2;
        const int OTA_CTRL_STATUS_WAIT_DATA = 3;

        const int OTA_CTRL_START = 0xAA; // param: no
        const int OTA_CTRL_PAGE_BEGIN = 0xB0; // param: page address, following DATA contains the data
        const int OTA_CTRL_PAGE_END = 0xB1; // param: no
        const int OTA_CTRL_READ_PAGE = 0xC0; // param: page address
        const int OTA_CTRL_SWITCH_APP = 0xD0; // param: no
        const int OTA_CTRL_METADATA = 0xE0; // param: ota_meta_t
        const int OTA_CTRL_REBOOT = 0xFF; // param: no

        string FUpdateURL;

        public string updateURL { get { return FUpdateURL; }
            set
            {
                FUpdateURL = value.Last() != '/' ? value + "/" : value;
            }
        }

        readonly IBleDriver driver;

        public event EventHandler<ProgressArgs> Progress;
        public event EventHandler<OTAStatus> StatusChanged;

        OTAStatus status = OTAStatus.Checking;

        Version Local;
        Version Latest;

        List<OTABin> Bins;
        OTABin MetaData;
        int Entry;

        public OTA(string url, IBleDriver driver)
        {
            updateURL = url;
            this.driver = driver;
            Bins = new List<OTABin>();
        }

        public bool Available { get { return driver.Available; } }
        public OTAStatus Status { get => status; }

        public string UpdateInfo { get; set; }

        public string LocalVersion { get { return Local != null ? Local.ToString() : "n/a"; } }
        public string LatestVersion { get { return Latest != null ? Latest.ToString() : "n/a"; } }

        void SetStatus(OTAStatus value)
        {
            if (value == status)
                return;
            status = value;
            StatusChanged.Invoke(this, status);
        }

        async Task ReadDevVer()
        {
            var v = await driver.ReadVer();
            if ((v == null) || (v.Length < 6))
                return;
            Local = new Version();
            Local.platform[0] = v[0 + 0] + (v[0 + 1] << 8);
            Local.platform[1] = v[0 + 2];
            Local.platform[2] = v[0 + 3];
            Local.app[0] = v[4 + 0] + (v[4 + 1] << 8);
            Local.app[1] = v[4 + 2];
            Local.app[2] = v[4 + 3];
        }

        public async Task ActivateSecondaryApp()
        {
            await driver.WriteCtrl(new byte[] { OTA_CTRL_SWITCH_APP });
        }

        public static async Task<Stream> URLGetStream(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            HttpWebResponse response;
            response = (HttpWebResponse)await request.GetResponseAsync();

            return response.GetResponseStream();
        }

        public static async Task<string> URLGet(string url)
        {
            Stream s = await URLGetStream(url);

            StreamReader reader = new StreamReader(s);
            return await reader.ReadToEndAsync();
        }

        public static async Task<byte[]> URLGetBytes(string url)
        {
            Stream s = await URLGetStream(url);

            using (var memoryStream = new MemoryStream())
            {
                s.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        async Task CheckZipPack(byte []bytes)
        {
            SetStatus(OTAStatus.Checking);
            await DecodePackage(bytes);
            MakeFlashProcedure();
            if ((Bins.Count > 0) && (Entry > 0))
                SetStatus(OTAStatus.UpdateAvailable);
            else
                SetStatus(OTAStatus.ServerError);
        }

        async public Task CheckUpdateLocal(byte[] bytes)
        {
            try
            {
                SetStatus(OTAStatus.Checking);
                await ReadDevVer();
                await CheckZipPack(bytes);
            }
            catch
            {
                SetStatus(OTAStatus.ServerError);
            }
        }

        public async Task CheckUpdate()
        {
            Latest = null;
            Local = null;
            Bins.Clear();
            Entry = 0;
            
            try
            { 
                SetStatus(OTAStatus.Checking);
                await ReadDevVer();
                var response = await URLGet(updateURL + "latest.json");
                Latest = JsonConvert.DeserializeObject<Version>(response);
                if ((Latest == null) || (Local == null))
                    throw new Exception("error");
                if (!NeedUpdate())
                {
                    SetStatus(OTAStatus.UpToDate);
                    return;
                }

                await CheckZipPack(await URLGetBytes(updateURL + Latest.package));
            }
            catch
            {
                SetStatus(OTAStatus.ServerError);
            }
        }

        Int64 ToUniqueNum(int[] ver)
        {
            return (ver[0] * 256 + ver[1]) * 256 + ver[2];
        }

        bool NeedUpdate()
        {
            var x1 = ToUniqueNum(Local.app);
            var x2 = ToUniqueNum(Latest.app);
            var y1 = ToUniqueNum(Local.platform);
            var y2 = ToUniqueNum(Latest.platform);
            return (x1 < x2) || (y1 < y2);
        }

        async Task AddBin(ZipArchive archive, string name, int address)
        {
            OTABin bin = new OTABin
            {
                Name = name,
                LoadAddress = address
            };
            Bins.Add(bin);

            MemoryStream buffer = new MemoryStream();
            await archive.GetEntry(bin.Name).Open().CopyToAsync(buffer);
            bin.Data = new byte[((buffer.Length + 3) / 4) * 4];
            buffer.Position = 0;
            buffer.Read(bin.Data, 0, (int)buffer.Length);
        }

        async Task DecodePackage(byte []bytes)
        {
            Stream stream = new MemoryStream(bytes);
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry entry = archive.GetEntry("readme");
                using (StreamReader reader = new StreamReader(entry.Open()))
                {
                    UpdateInfo = await reader.ReadToEndAsync();
                }

                entry = archive.GetEntry("manifest.json");
                dynamic manifest;
                using (StreamReader reader = new StreamReader(entry.Open()))
                {
                    manifest = JsonConvert.DeserializeObject<dynamic>(await reader.ReadToEndAsync());
                    Entry = manifest.entry;
                }

                if (Latest == null)
                {
                    Latest = new Version();
                    for (int i = 0; i < Latest.app.Length; i++)
                        Latest.app[i] = (int)manifest.app.version[i];
                    for (int i = 0; i < Latest.platform.Length; i++)
                        Latest.platform[i] = (int)manifest.platform.version[i];
                }

                bool up_platform = false;
                if (ToUniqueNum(Local.platform) != ToUniqueNum(Latest.platform))
                {
                    up_platform = true;
                    await AddBin(archive, (string)manifest.platform.name, (int)manifest.platform.address);
                }

                if (up_platform || (ToUniqueNum(Local.app) != ToUniqueNum(Latest.app)))
                    await AddBin(archive, (string)manifest.app.name, (int)manifest.app.address);

                foreach (dynamic x in manifest.bins)
                    await AddBin(archive, (string)x.name, (int)x.address);
            }
        }

        protected virtual void MakeFlashProcedure()
        {
            int addr = FLASH_OTA_DATA_HIGH;
            if (Bins.Count < 1)
                return;

            foreach (var b in Bins)
            {
                addr -= ((b.Data.Length + FLASH_PAGE_SIZE - 1) / FLASH_PAGE_SIZE) *FLASH_PAGE_SIZE;
                b.WriteAddress = addr;
            }

            var update = new byte [2 + 4 + Bins.Count * 4 * 3];

            int c = 2;
            Utils.WriteLittle((UInt32)Entry, update, c); c += 4;
            foreach (var b in Bins)
            {
                Utils.WriteLittle((UInt32)b.WriteAddress, update, c); c += 4;
                Utils.WriteLittle((UInt32)b.LoadAddress, update, c); c += 4; 
                Utils.WriteLittle((UInt32)b.Data.Length, update, c); c += 4;                              
            }

            var crc = Utils.Crc(update.AsSpan(2).ToArray());
            update[0] = (byte)(crc & 0xff);
            update[1] = (byte)(crc >> 8);

            MetaData = new OTABin();
            MetaData.Data = update;
            MetaData.Name = "metadata";
        }

        async Task<bool> CheckDevStatus()
        {
            var r = await driver.ReadCtrl();
            if ((r == null) || (r.Length < 1))
                return false;
            return r[0] == OTA_CTRL_STATUS_OK;
        }

        async Task<int> ReadStatus()
        {
            var r = await driver.ReadCtrl();
            if ((r == null) || (r.Length < 1))
                return OTA_CTRL_STATUS_ERROR;
            return r[0];
        }

        void SendProgress(ProgressArgs e, string msg)
        {
            e.Msg = msg;
            Progress.Invoke(this, e);
        }

        void SendProgress(ProgressArgs e, double value)
        {
            e.Progress = value;
            Progress.Invoke(this, e);
        }

        void SendProgress(ProgressArgs e, int msg, double value)
        {
            e.Progress = value;
            e.Msg = msg.ToString();
            Progress.Invoke(this, e);
        }

        async Task<bool> BurnPage(ProgressArgs progress,  byte[]page, Action<int> onProgress)
        {            
            int current = 0;
            while (current < page.Length)
            {
                onProgress(current);

                int size = Math.Min(OTA_BLOCK_SIZE, page.Length - current);
                var block = new byte[size];
                Array.Copy(page, current, block, 0, size);
                if (!await driver.WriteData(block)) return false;
                current += size;
                await Task.Delay(Math.Max(1, (OTA_BLOCK_SIZE + 3) / 4 * 34 / 1000));  // 34us per 4 byte
            }

            var cmd = new byte[] { OTA_CTRL_PAGE_END, 0, 0, 0, 0 };
            uint param = (uint)(Utils.Crc(page) << 16 | page.Length);
            Utils.WriteLittle(param, cmd, 1);

            if (!await driver.WriteCtrl(cmd)) return false;

            while (true)
            {
                await Task.Delay(10);
                switch (await ReadStatus())
                {
                    case OTA_CTRL_STATUS_ERROR: 
                        return false;
                    case OTA_CTRL_STATUS_OK:
                        return true;
                }
            }            
        }

        async Task<bool> BurnMetaData()
        {
            var progress = new ProgressArgs() { Status = UpdateStatus.Running };

            SendProgress(progress, String.Format("burn {0} ...", MetaData.Name));

            var cmd = new byte[1 + MetaData.Data.Length];
            cmd[0] = OTA_CTRL_METADATA;
            Array.Copy(MetaData.Data, 0, cmd, 1, MetaData.Data.Length);

            if (!await driver.WriteCtrl(cmd)) return false;
            return await CheckDevStatus();
        }

        async Task<bool> BurnFiles()
        {
            var progress = new ProgressArgs() { Status = UpdateStatus.Running };
            int errors = 0;
            int total = Bins.Select((b) => b.Data.Length).Sum();
            int written = 0;

            Action<int> onProgress = (int current) => SendProgress(progress, (double)(current + written) / total);            

            foreach (var b in Bins)
            {
                int offset = 0;
                while (offset < b.Data.Length)
                {
                    SendProgress(progress, String.Format("prepare new page for {0} ...", b.Name));

                    var cmd = new byte[] { OTA_CTRL_PAGE_BEGIN, 0, 0, 0, 0 };
                    Utils.WriteLittle((UInt32)(offset + b.WriteAddress), cmd, 1);

                    if (!await driver.WriteCtrl(cmd)) return false;
                    if (!await CheckDevStatus()) return false;

                    var page = new byte[Math.Min(FLASH_PAGE_SIZE, b.Data.Length - offset)];
                    Array.Copy(b.Data, offset, page, 0, page.Length);

                    if (errors > 0)
                        SendProgress(progress, String.Format("burn {0} ... (retry #{1})", b.Name, errors));
                    else
                        SendProgress(progress, String.Format("burn {0} ...", b.Name));

                    if (await BurnPage(progress, page, onProgress))
                    {
                        offset += page.Length;
                        written += page.Length;
                        errors = 0;
                    }
                    else
                    {
                        errors++;
                        if (errors > 5)
                        {
                            if ((written == 0) && (OTA_BLOCK_SIZE > Utils.BLE_MIN_MTU_SIZE))
                            {
                                SendProgress(progress, "fallback to MTU size = minimum (23)...");                                
                                OTA_BLOCK_SIZE = Utils.BLE_MIN_MTU_SIZE;
                                errors = 0;
                                await Task.Delay(20);
                            }
                            return false;
                        }
                        SendProgress(progress, "error occurs, retry...");
                    }
                }
            }
            return true;
        }

        async Task<bool> DoStart()
        {
            var progress = new ProgressArgs() { Status = UpdateStatus.Running };
            SendProgress(progress, "enable FOTA...");
            await driver.WriteCtrl(new byte[] { OTA_CTRL_START, 0, 0, 0, 0 });
            if (!await CheckDevStatus())
                throw new Exception("failed to enable FOTA");
            SendProgress(progress, "FOTA successfully enabled");
            if (!await BurnFiles())
                throw new Exception("burn failed");
            if (!await BurnMetaData())
                throw new Exception("metadata failed");
            SendProgress(progress, "FOTA burn complete, reboot...");
            await driver.WriteCtrl(new byte[] { OTA_CTRL_REBOOT });
            return true;
        }

        async public Task<bool> Update(int MtuSize = 23)
        {
            bool r = false;
            OTA_BLOCK_SIZE = (MtuSize - 3) & 0xFFFC;
            var progress = new ProgressArgs() { Status = UpdateStatus.Done };
            try
            {
                if (OTA_BLOCK_SIZE < Utils.BLE_MIN_MTU_SIZE)
                    throw new Exception("MtuSize must be >= 20");
                r = await DoStart();           
            }
            catch (Exception e)
            {
                r = false;
                progress.Msg = e.Message;
                progress.Status = UpdateStatus.Error;
            }
            Progress.Invoke(this, progress);
            return r;
        }
    }
}
