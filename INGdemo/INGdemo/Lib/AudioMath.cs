using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace INGdemo.Lib
{
    static class Constants
    {
        public const int MSBC_BLOCKS = 15;
        public const int SBCDEC_FIXED_EXTRA_BITS = 2;
        public const int SBC_HEADER_SIZE = 4;

        /* sampling frequency */
        public const byte SBC_FREQ_16000 = 0x00;
        public const byte SBC_FREQ_32000 = 0x01;
        public const byte SBC_FREQ_44100 = 0x02;
        public const byte SBC_FREQ_48000 = 0x03;

        /* blocks */
        public const byte SBC_BLK_4 = 0x00;
        public const byte SBC_BLK_8 = 0x01;
        public const byte SBC_BLK_12 = 0x02;
        public const byte SBC_BLK_16 = 0x03;

        /* channel mode */
        public const byte SBC_MODE_MONO = 0x00;
        public const byte SBC_MODE_DUAL_CHANNEL = 0x01;
        public const byte SBC_MODE_STEREO = 0x02;
        public const byte SBC_MODE_JOINT_STEREO = 0x03;

        /* allocation method */
        public const byte SBC_AM_LOUDNESS = 0x00;
        public const byte SBC_AM_SNR = 0x01;

        /* subbands */
        public const byte SBC_SB_4 = 0x00;
        public const byte SBC_SB_8 = 0x01;

        /* data endian */
        public const byte SBC_LE = 0x00;
        public const byte SBC_BE = 0x01;

        public const int  SCALE_SPROTO4_TBL = 12;
        public const int  SCALE_SPROTO8_TBL = 14;
        public const int  SCALE_NPROTO4_TBL = 11;
        public const int  SCALE_NPROTO8_TBL = 11;

        public const int SCALE4_STAGED1_BITS = 15;
        public const int SCALE4_STAGED2_BITS = 16;
        public const int SCALE8_STAGED1_BITS = 15;
        public const int SCALE8_STAGED2_BITS = 16;


        public const byte SBC_SYNCWORD = 0x9C;
    }

    public class exp
    {
        static public int CI(uint i)
        {
            if (i <= 0x7fffffff)
                return (int)i;
            else
            {
                return -(int)(~i) - 1;
            }
        }

        static public int SS4(int i)
        {
            return i >> Constants.SCALE_SPROTO4_TBL;
        }

        static public int SS8(int i)
        {
            return i >> Constants.SCALE_SPROTO8_TBL;
        }

        static public int SN4(int i)
        {
            return  i >> (Constants.SCALE_NPROTO4_TBL + 1 + Constants.SBCDEC_FIXED_EXTRA_BITS);
        }

        static public int SN8(int i)
        {
            return  i >> (Constants.SCALE_NPROTO8_TBL + 1 + Constants.SBCDEC_FIXED_EXTRA_BITS);
        }

        static public int SN8(uint i)
        {
            return SN8(CI(i));
        }

        static public int MUL(int a, int b)
        {
            return a * b;
        }

        static public int MULA(int a, int b, int res)
        {
            return a * b + res;
        }

        static public int SCALE4_STAGED1(int i)
        {
            return i >> Constants.SCALE4_STAGED1_BITS;
        }

        static public int SCALE4_STAGED2(int i)
        {
            return i >> Constants.SCALE4_STAGED2_BITS;
        }

        static public int SCALE8_STAGED1(int i)
        {
            return i >> Constants.SCALE8_STAGED1_BITS;
        }

        static public int SCALE8_STAGED2(int i)
        {
            return i >> Constants.SCALE8_STAGED2_BITS;
        }

        static public short sbc_clip16(int i)
        {
            if (i > 0x7FFF)    //32767
                return 0x7FFF;
            else if (i < -0x8000)  //-32768
                return -0x8000;
            else
                return (short)i;
        }

        static public byte sbc_crc8(byte[] data, int len)
        {
            byte crc = 0x0f;
            int i;
            byte octet;

            for (i = 0; i < len / 8; i++)
                crc = SbcCRC.crc_table[crc ^ data[i]];

            octet = (byte)((len % 8 == 0) ?  0 : data[i]);
            for (i = 0; i < len % 8; i++) {
                byte bit = (byte)(((octet ^ crc) & 0x80) >> 7);

                crc = (byte)(((crc & 0x7f) << 1) ^ (bit == 0 ? 0 : 0x1d));

                octet = (byte)(octet << 1);
            }

            return crc;
        }
    }
}