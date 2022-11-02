using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace INGdemo.Lib
{   
    public enum Channels
    {
        
        MONO		= Constants.SBC_MODE_MONO,
        DUAL_CHANNEL	= Constants.SBC_MODE_DUAL_CHANNEL,
        STEREO		= Constants.SBC_MODE_STEREO,
        JOINT_STEREO	= Constants.SBC_MODE_JOINT_STEREO
    }

    public enum Allocate
    {
        LOUDNESS	= Constants.SBC_AM_LOUDNESS,
        SNR		= Constants.SBC_AM_SNR
    }



    public struct sbc_frame {
        public byte frequency;
        public byte block_mode;
        public byte blocks;
        public Channels mode;
        public byte channels;
        public Allocate allocation;
        public byte subband_mode;
        public byte subbands;
        public byte bitpool;
        public ushort codesize;
        public ushort length;
        public ushort frame_count;

        /* bit number x set means joint stereo has been used in subband x */
        public byte joint;

        /* only the lower 4 bits of every element are to be used */
        public uint[,]  scale_factor;

        /* raw integer subband samples in the frame */
        public int[,,] sb_sample_f;

        /* modified subband samples */
        public int[,,] sb_sample;

        /* original pcm audio samples */
        public short[,] pcm_sample; 
     
    }

    public struct sbc_decoder_state
    {
        public int subbands;
        public int[,] V;
        public int[,] offset;
    }



    public struct sbc_priv
    {
        public bool init;
        public sbc_frame frame;
        public sbc_decoder_state dec_state;
    }

 


    public struct sbc_struct
    {
        public ulong flags;

        public byte frequency;
        public byte blocks;
        public byte subbands;
        public byte mode;
        public byte allocation;
        public byte bitpool;
        public byte endian;

        public sbc_priv priv;     
    }


}