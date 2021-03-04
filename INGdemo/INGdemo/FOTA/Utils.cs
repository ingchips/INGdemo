using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Xamarin.Forms;
using System.Globalization;

namespace INGota.FOTA
{
    class Utils
    {
        public const int BLE_MIN_MTU_SIZE = 20;

        static public int MTU2AttSize(int size)
        {
            return size - 3;
        }

        static public int Att2MTUSize(int size)
        {
            return size + 3;
        }

        static public String GetDeviceAddress(Guid guid)
        {
            String r = "";
            var v = guid.ToByteArray();
            for (int i = v.Length - 6; i < v.Length; i++)
                r = r + string.Format("{0:X2}:", v[i]);
            return r.Substring(0, r.Length - 1);
        }

        static public List<Guid> ConvertFromUUID16(byte []data)
        {
            var r = new List<Guid>();
            var baseId = new Guid("00000000-0000-1000-8000-00805F9B34FB").ToByteArray();
            for (int i = 0; i <= data.Length - 2; i += 2)
            {
                baseId[1] = data[i];
                baseId[0] = data[i + 1];
                r.Add(new Guid(baseId));
            }
            return r;
        }

        static public int GetAdvFlags(IEnumerable<AdvertisementRecord> advs)
        {
            var adv = advs.Where((x) => x.Type == AdvertisementRecordType.Flags).FirstOrDefault();
            return adv != null ? adv.Data[0] : 0;
        }

        static public bool IsConnectable(IEnumerable<AdvertisementRecord> advs)
        {
            if ((Utils.GetAdvFlags(advs) & 0x3) != 0)
                return true;
            var adv = advs.Where((x) => x.Type == AdvertisementRecordType.IsConnectable).FirstOrDefault();
            return adv != null ? adv.Data[0] != 0 : false;
        }

        static public List<Guid> GetServices(IEnumerable<AdvertisementRecord> advs)
        {
            var r = new List<Guid>();
            foreach (var adv in advs)
            {
                switch (adv.Type)
                {
                    case AdvertisementRecordType.UuidsComplete16Bit:
                    case AdvertisementRecordType.UuidsIncomple16Bit:
                        r.AddRange(ConvertFromUUID16(adv.Data));
                        break;
                    case AdvertisementRecordType.UuidsComplete128Bit:
                        var j = 0;
                        for (; j <= adv.Data.Length - 16; j += 16)
                        {
                            r.Add(fromBleBytes(adv.Data, j));
                        }
                        break;
                }
            }
            return r;
        }

        static public String ByteArrayToString(byte []array)
        {
            return String.Join(", ", array.Select((b) => String.Format("{0:X2}", b)));
        }

        static public void WriteLittleBytes(byte[] bytes, byte[] buffer, int start)
        {
            var t = bytes;
            if (!BitConverter.IsLittleEndian)
                t = bytes.Reverse().ToArray();
            for (int i = 0; i < t.Length; i++)
                buffer[start + i] = t[i];
        }

        static public void WriteLittle(UInt32 value, byte []buffer, int start)
        {
            WriteLittleBytes(BitConverter.GetBytes(value), buffer, start);
        }

        static public Int64 ParseBigInt(IEnumerable<byte> buffer)
        {
            Int64 r = 0;
            foreach (var x in buffer) r = r * 256 + x;
            return r;
        }

        static public Int64 ParseLittleInt(IEnumerable<byte> buffer)
        {
            return ParseBigInt(buffer.Reverse());
        }

        static public Guid fromBleBytes(byte []buffer, int start)
        {
            return Guid.Parse(string.Format("{0:X2}{1:X2}{2:X2}{3:X2}-{4:X2}{5:X2}-{6:X2}{7:X2}-{8:X2}{9:X2}-{10:X2}{11:X2}{12:X2}{13:X2}{14:X2}{15:X2}",
                buffer[start + 0], buffer[start + 1], buffer[start + 2], buffer[start + 3],
                buffer[start + 4], buffer[start + 5],
                buffer[start + 6], buffer[start + 7],
                buffer[start + 8], buffer[start + 9],
                buffer[start + 10], buffer[start + 11], buffer[start + 12], buffer[start + 13], buffer[start + 14], buffer[start + 15]));
        }

        static public String DecodeAdvertisement(AdvertisementRecord adv)
        {
            switch (adv.Type)
            {
                case AdvertisementRecordType.Flags:
                    var x = new String[]{ "LE Limited Discoverable Mode",
                                            "LE General Discoverable Mode",
                                            "BR/EDR Not Supported",
                                            "Simultaneous LE and BR/EDR (Controller)",
                                            "Simultaneous LE and BR/EDR (Host)"};

                    return String.Join(", ", x.Where((_, i) => (adv.Data[0] & (1 << i)) != 0));
                case AdvertisementRecordType.UuidsComplete16Bit:
                case AdvertisementRecordType.UuidsIncomple16Bit:
                    var list = ConvertFromUUID16(adv.Data);
                    return String.Join(", ", list.Select((guid) => guid.ToString()));
                case AdvertisementRecordType.UuidsComplete128Bit:
                    var j = 0;
                    string s = "";
                    for (; j <= adv.Data.Length - 16; j += 16)
                    {
                        s += fromBleBytes(adv.Data, j).ToString() + "\n";
                    }
                    s.TrimEnd(new char []{'\n'});
                    return s;
                case AdvertisementRecordType.ShortLocalName:
                case AdvertisementRecordType.CompleteLocalName:                    
                    return Encoding.UTF8.GetString(adv.Data);
                default:
                    return ByteArrayToString(adv.Data);
            }
        }

        static public string float_ieee_11073_val_to_repr(UInt32 val)
        {
            const int FLOAT_VALUE_INFINITY_PLUS = 0x007FFFFE;
            const int FLOAT_VALUE_NAN = 0x007FFFFF;
            const int FLOAT_VALUE_NRES = 0x00800000;
            const int FLOAT_VALUE_RFU = 0x00800001;
            const int FLOAT_VALUE_INFINITY_MINUS = 0x00800002;

            if (val >= 0x007FFFFE && val <= 0x00800002)
            {
                switch (val)
                {
                    case FLOAT_VALUE_INFINITY_PLUS:
                        return "+INFINITY";
                    case FLOAT_VALUE_NAN:
                        return "NaN";
                    case FLOAT_VALUE_NRES:
                        return "NRes";
                    case FLOAT_VALUE_RFU:
                        return "RFU";
                    case FLOAT_VALUE_INFINITY_MINUS:
                        return "-INFINITY";
                    default:
                        return "BAD ieee_11073_val";
                }
            }

            int exponent = (SByte)(val >> 24);
            int mantissa = (int)(val & 0x7FFFFF);

            if ((val & 0x00800000) != 0)
                mantissa -= 0x00800000;

            if (mantissa == 0) return "0";

            string r = mantissa.ToString();
            if (exponent > 0)
            {
                r += '0' * exponent;
            }
            else if (exponent < 0)
            {
                exponent = -exponent;
                if (exponent < r.Length)
                    r = r.Substring(0, r.Length - exponent) + '.' + r.Substring(r.Length - exponent);
                else
                    r = "0." + r.PadLeft(exponent, '0');
            }

            return r;
        }

        static public string GetMonoFamily()
        {
            var MonoFamily = "Courier";
            switch (Device.RuntimePlatform)
            {
                case Device.iOS:
                    MonoFamily = "Menlo";
                    break;
                case Device.Android:
                    // Lots of androids don't have this *standard* mono font
                    //MonoFamily = "Droid Sans Mono";
                    break;
                case Device.UWP:
                    MonoFamily = "Consolas";
                    break;
            }
            return MonoFamily;
        }

        static readonly byte[] auchCRCHi = new byte[]
        {
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40
        };

        static readonly byte[] auchCRCLo = new byte[]
        {
            0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7, 0x05, 0xC5, 0xC4, 0x04,
            0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09, 0x08, 0xC8,
            0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD, 0x1D, 0x1C, 0xDC,
            0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3, 0x11, 0xD1, 0xD0, 0x10,
            0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32, 0x36, 0xF6, 0xF7, 0x37, 0xF5, 0x35, 0x34, 0xF4,
            0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A, 0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38,
            0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE, 0x2E, 0x2F, 0xEF, 0x2D, 0xED, 0xEC, 0x2C,
            0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26, 0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0,
            0xA0, 0x60, 0x61, 0xA1, 0x63, 0xA3, 0xA2, 0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4,
            0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F, 0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68,
            0x78, 0xB8, 0xB9, 0x79, 0xBB, 0x7B, 0x7A, 0xBA, 0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C,
            0xB4, 0x74, 0x75, 0xB5, 0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0,
            0x50, 0x90, 0x91, 0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54,
            0x9C, 0x5C, 0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98,
            0x88, 0x48, 0x49, 0x89, 0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
            0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80, 0x40
        };

        public static UInt16 Crc(IEnumerable<byte> data)
        {
            byte hi = 0xFF; /* high byte of CRC initialized */
            byte lo = 0xFF; /* low byte of CRC initialized */

            foreach (var x in data)
            {
                byte uIndex = (byte)(hi ^ x); /* calculate the CRC */
                hi = (byte)(lo ^ auchCRCHi[uIndex]);
                lo = auchCRCLo[uIndex];
            }

            return (UInt16)(hi << 8 | lo);
        }

        public static byte[] ParseBytes(string str)
        {
            var l = new List<byte>();
            foreach (var s in str.Split(new char[] { ',', ';', ' ', ':', '\n', '\r' }))
            {
                byte value;
                if (byte.TryParse(s, NumberStyles.HexNumber,
                                    CultureInfo.CurrentCulture,
                                    out value))
                    l.Add(value);
            }
            return l.ToArray();
        }

        public static string PrintHexTable(byte []v)
        {
            var rows = (v.Length + 15) / 16;
            var r = new string[rows + 2];
            r[0] = " 0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F  | 0123456789ABCDEF";
            r[1] = "-------------------------------------------------------------------";
            for (var i = 0; i < rows; i++)
            {
                StringBuilder str = new StringBuilder("                                                   ................");
                for (var j = 0; j < 16; j++)
                {
                    var k = i * 16 + j;
                    if (k >= v.Length) break;
                    var b = string.Format("{0:X2}", v[k]);
                    str[j * 3 + 0] = b[0];
                    str[j * 3 + 1] = b[1];
                    if ((0x20 <= v[k]) && (v[k] <= 0x7e))
                    {
                        str[16 * 3 + 3 + j] = char.ConvertFromUtf32(v[k])[0];
                    }
                }
                r[i + 2] = str.ToString();
            }
            return string.Join("\n", r);
        }
    }
}
