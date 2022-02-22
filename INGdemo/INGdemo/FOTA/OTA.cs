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
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Asn1.Sec;

namespace INGota.FOTA
{
    internal class OTABin
    {
        internal int WriteAddress = 0;
        internal int LoadAddress = 0;
        internal byte[] Data;
        internal string Name;
    }

    internal readonly struct FlashInfo
    {
        internal FlashInfo(UInt32 BaseAddr, UInt32 TotalSize, UInt32 PageSize, bool ManualReboot)
        {
            this.BaseAddr = BaseAddr;
            this.TotalSize = TotalSize;
            this.PageSize = PageSize;
            this.ManualReboot = ManualReboot;
        }
        internal readonly UInt32 BaseAddr;
        internal readonly UInt32 TotalSize;
        internal readonly UInt32 PageSize;
        internal readonly bool ManualReboot;
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

    internal class KeyUtils
    {
        public static byte[] root_sk = new byte[] {
            0x5c, 0x77, 0x17, 0x11, 0x67, 0xd6, 0x40, 0xa3, 0x36, 0x0d, 0xe2,
            0x69, 0xfe, 0x0b, 0xb7, 0x8f, 0x5e, 0x94, 0xd8, 0xf2, 0xf4, 0x80,
            0x94, 0x0a, 0xc2, 0xf2, 0x6e, 0x43, 0xbb, 0x69, 0x5f, 0xa7};
        public static byte[] root_pk = new byte[] {
            0x14, 0x1b, 0x0b, 0x28, 0x46, 0xc4, 0xaf, 0x97, 0x41, 0x59, 0x97, 
            0x4f, 0x17, 0x52, 0xe0, 0x1c, 0x9a, 0xea, 0x21, 0xc7, 0xc6, 0xe3, 
            0x04, 0x30, 0x4f, 0x8d, 0x9c, 0xf0, 0x7f, 0x1d, 0x1f, 0x0a, 0x83, 
            0xaf, 0x76, 0xe0, 0x4d, 0xc1, 0xcc, 0x96, 0xb4, 0xb8, 0x3f, 0xbb, 
            0x73, 0x6c, 0x66, 0x3f, 0x0b, 0xdf, 0x52, 0x86, 0xbf, 0x60, 0xe8, 
            0x91, 0x27, 0x00, 0x85, 0xc8, 0xbf, 0x55, 0xa8, 0x96};
        public byte[] session_pk;
        public byte[] session_sk;
        public byte[] peer_pk;
        public byte[] shared_secret;
        public byte[] xor_key;
        public bool is_secure_fota;

        public static T[] SubArray<T>(T[] array, int offset, int length)
        {
            T[] result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }

        public KeyUtils()
        {
            var conf = new ECKeyGenerationParameters(SecObjectIdentifiers.SecP256r1, new SecureRandom());
            var keyGen = new ECKeyPairGenerator("ECDSA");
            keyGen.Init(conf);

            var keyPair = keyGen.GenerateKeyPair();
            session_sk = ((ECPrivateKeyParameters)keyPair.Private).D.ToByteArrayUnsigned();
            var pk = (ECPublicKeyParameters)keyPair.Public;
            session_pk = pk.Q.XCoord.ToBigInteger().ToByteArrayUnsigned().Concat(pk.Q.YCoord.ToBigInteger().ToByteArrayUnsigned()).ToArray();
            is_secure_fota = false;
        }

        public byte [] SignData(byte []sk, byte[]data)
        {
            var curve = NistNamedCurves.GetByName("P-256");
            var ecParam = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());
            var privKey = new ECPrivateKeyParameters(new BigInteger(1, sk), ecParam);

            var hash = SHA256(data);
            var sa = new ECDsaSigner();
            sa.Init(true, privKey);
            var sig = sa.GenerateSignature(hash);
            var b1 = sig[0].ToByteArrayUnsigned();
            var b2 = sig[1].ToByteArrayUnsigned();
            var r = new byte[b1.Length + b2.Length];
            Array.Copy(b1, 0, r, 0, b1.Length);
            Array.Copy(b2, 0, r, b1.Length, b2.Length);
            return r;
        }

        public void Encrypt(byte []data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] ^= xor_key[i & 0x1f];
        }

        public static byte[] SHA256(byte []data)
        {
            var bcsha256a = new Sha256Digest();
            bcsha256a.BlockUpdate(data, 0, data.Length);

            byte[] checksum = new byte[32];
            bcsha256a.DoFinal(checksum, 0);
            return checksum;
        }

        public static byte[] getSharedSecret(byte[] PrivateKeyIn, byte[] PublicKeyIn)
        {
            ECDHCBasicAgreement agreement = new ECDHCBasicAgreement();
            X9ECParameters curve = null;
            ECDomainParameters ecParam = null;
            ECPrivateKeyParameters privKey = null;
            ECPublicKeyParameters pubKey = null;

            curve = NistNamedCurves.GetByName("P-256");
            ecParam = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());
            privKey = new ECPrivateKeyParameters(new BigInteger(1, PrivateKeyIn), ecParam);

            BigInteger X = new BigInteger(1, PublicKeyIn, 0, 32);
            BigInteger Y = new BigInteger(1, PublicKeyIn, 32, 32);

            pubKey = new ECPublicKeyParameters(curve.Curve.ValidatePoint(X, Y), ecParam);

            agreement.Init(privKey);

            BigInteger secret = agreement.CalculateAgreement(pubKey);

            return secret.ToByteArrayUnsigned();
        }
    }

    class OTA
    {
        int OTA_BLOCK_SIZE = 20;

        FlashInfo[] FlashInfos = new FlashInfo[]
        {
             new FlashInfo(0x4000U, 512 * 1024, 8 * 1024, true),        // ING9188
             new FlashInfo(0x4000U, 256 * 1024, 8 * 1024, true),        // ING9186
             new FlashInfo(0x02000000U, 2048 * 1024, 4 * 1024, false),  // ING9168
        };

        FlashInfo CurrentFlash;

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
            Idle,
            Checking,
            UpToDate,
            UpdateAvailable,
            ServerError
        }

        int FLASH_BASE { get { return (int)CurrentFlash.BaseAddr; } }
        int FLASH_SIZE { get { return (int)CurrentFlash.TotalSize; } }
        int FLASH_PAGE_SIZE { get { return (int)CurrentFlash.PageSize; } }
        int FLASH_OTA_DATA_HIGH { get { return (int)CurrentFlash.BaseAddr + (int)CurrentFlash.TotalSize; } }

        const int OTA_UPDATE_FLAG = 0x5A5A5A5A;
        const int OTA_LOCK_FLAG = 0x5A5A5A5A;        

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
        KeyUtils KeyUtils;

        public string updateURL { get { return FUpdateURL; }
            set
            {
                FUpdateURL = value.Last() != '/' ? value + "/" : value;
            }
        }

        readonly IBleDriver driver;

        public event EventHandler<ProgressArgs> Progress;
        public event EventHandler<OTAStatus> StatusChanged;

        OTAStatus status = OTAStatus.Idle;

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
            KeyUtils = new KeyUtils();
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

        async Task<bool> ExchangeKeys()
        {
            KeyUtils.peer_pk = await driver.ReadPubKey();
            var sig = KeyUtils.SignData(KeyUtils.root_sk, KeyUtils.session_pk);
            if (!await driver.WritePubKey(KeyUtils.session_pk.Concat(sig).ToArray())) return false;            
            var r = (await ReadStatus()) != OTA_CTRL_STATUS_ERROR;
            if (r)
            {
                KeyUtils.shared_secret = KeyUtils.getSharedSecret(KeyUtils.session_sk, KeyUtils.peer_pk);
                KeyUtils.xor_key = KeyUtils.SHA256(KeyUtils.shared_secret);
                KeyUtils.is_secure_fota = true; 
            }
            return r;
        }

        async Task ReadDevVer()
        {
            if (driver.IsSecure && !(await ExchangeKeys()))
                return;

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
            if (Bins.Count > 0)
                SetStatus(OTAStatus.UpdateAvailable);
            else
                SetStatus(OTAStatus.UpToDate);
        }

        async public Task CheckUpdateLocal(int series, byte[] bytes)
        {
            Latest = null;
            Local = null;
            CurrentFlash = FlashInfos[series];
            Bins.Clear();
            Entry = 0;
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

        public async Task CheckUpdate(int series)
        {
            Latest = null;
            Local = null;
            CurrentFlash = FlashInfos[series];
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

            if (KeyUtils.is_secure_fota)
            {
                var sig = KeyUtils.SignData(KeyUtils.session_sk, update.AsSpan(2).ToArray());
                var new_update = new byte[sig.Length + update.Length];
                Array.Copy(sig, 0, new_update, 0, sig.Length);
                var enc_data = update.AsSpan(2).ToArray();
                KeyUtils.Encrypt(enc_data);
                Array.Copy(enc_data, 0, new_update, sig.Length + 2, enc_data.Length);
                var crc = Utils.Crc(enc_data);
                new_update[sig.Length + 0] = (byte)(crc & 0xff);
                new_update[sig.Length + 1] = (byte)(crc >> 8);
                update = new_update;
            }
            else
            {
                var crc = Utils.Crc(update.AsSpan(2).ToArray());
                update[0] = (byte)(crc & 0xff);
                update[1] = (byte)(crc >> 8);
            }

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

        async Task<bool> BurnPage(ProgressArgs progress,  byte[]page, Action<int> onProgress, UInt32 address)
        {
            byte [] sig = null;
            if (KeyUtils.is_secure_fota)
            {
                sig = KeyUtils.SignData(KeyUtils.session_sk, page);
                KeyUtils.Encrypt(page);
            }

            var cmd = new byte[] { OTA_CTRL_PAGE_BEGIN, 0, 0, 0, 0 };
            Utils.WriteLittle(address, cmd, 1);

            if (!await driver.WriteCtrl(cmd)) return false;
            if (!await CheckDevStatus()) return false;

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

            if (KeyUtils.is_secure_fota)
            {
                cmd = new byte[1 + 4 + sig.Length];
                cmd[0] = OTA_CTRL_PAGE_END;
                uint param = (uint)(Utils.Crc(page) << 16 | page.Length);
                Utils.WriteLittle(param, cmd, 1);
                Array.Copy(sig, 0, cmd, 5, sig.Length);
            }
            else
            {
                cmd = new byte[] { OTA_CTRL_PAGE_END, 0, 0, 0, 0 };
                uint param = (uint)(Utils.Crc(page) << 16 | page.Length);
                Utils.WriteLittle(param, cmd, 1);
            }
            
            if (!await driver.WriteCtrl(cmd)) return false;

            while (true)
            {
                await Task.Delay(KeyUtils.is_secure_fota ? 200 : 10);
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
            return CurrentFlash.ManualReboot ? await CheckDevStatus() : true;
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

                    var page = new byte[Math.Min(FLASH_PAGE_SIZE, b.Data.Length - offset)];
                    Array.Copy(b.Data, offset, page, 0, page.Length);

                    if (errors > 0)
                        SendProgress(progress, String.Format("burn {0} ... (retry #{1})", b.Name, errors));
                    else
                        SendProgress(progress, String.Format("burn {0} ...", b.Name));

                    if (await BurnPage(progress, page, onProgress, (UInt32)(offset + b.WriteAddress)))
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
            if (CurrentFlash.ManualReboot)
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
