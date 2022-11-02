using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace INGdemo.Lib
{     
    public class SBCDecoder
    {
        //Queue<byte> inputStream = new Queue<byte>();  //初始化输入队列
        private int inputSize = 24;
        private int outputSize = 128;
        private int Readindex;
        private int WriteIndex;
        private byte[] inputStream;
        private byte[] outputStream;
        private int inp = 0;  //解码器输入数组位置指示器,初始化为0
        private int outp = 0;
        private int decoded;
        private bool dec_lock = true;
        public static sbc_struct sbc;

        public SBCDecoder()
        {
            Reset();
        }

        public event EventHandler<byte []> SBCOutput;

        public void Reset()
        {
            inputStream = new byte[inputSize];
            outputStream = new byte[outputSize];
            Readindex = 0;
            WriteIndex = 0;

            //frame结构体对象初始化
            sbc.priv.frame.scale_factor = new uint[2,8];
            sbc.priv.frame.sb_sample_f = new int[16,2,8];
            sbc.priv.frame.sb_sample = new int[16,2,8];
            sbc.priv.frame.pcm_sample = new short[2,16*8];

            //sbc_decoder
            sbc.priv.dec_state.V = new int[2,170];
            sbc.priv.dec_state.offset = new int[2,16];
        }
        int sbc_decode(byte[] data, int input_len, byte[] output, int output_len, int written)
        {
            System.Diagnostics.Debug.WriteLine("sbc_decode()!");
            int i, ch, codesize, samples;
            codesize = sbc_unpack_frame(data, sbc.priv.frame, input_len);

            if (!sbc.priv.init) {
                //初始化解码器
                sbc_decoder_init(sbc.priv.dec_state, sbc.priv.frame);

                sbc.frequency = sbc.priv.frame.frequency;
                sbc.mode = sbc.priv.frame.mode == Channels.MONO ? Constants.SBC_MODE_MONO :
                                    sbc.priv.frame.mode == Channels.DUAL_CHANNEL ? Constants.SBC_MODE_DUAL_CHANNEL :
                                        sbc.priv.frame.mode == Channels.STEREO ? Constants.SBC_MODE_STEREO : Constants.SBC_MODE_JOINT_STEREO;
                sbc.subbands = sbc.priv.frame.subband_mode;
                sbc.blocks = sbc.priv.frame.block_mode;
                sbc.allocation = sbc.priv.frame.allocation == Allocate.SNR ? Constants.SBC_AM_SNR : Constants.SBC_AM_LOUDNESS;
                sbc.bitpool = sbc.priv.frame.bitpool;

                sbc.priv.frame.codesize = sbc_get_dec_codesize(sbc);
                sbc.priv.frame.length = sbc_get_dec_frame_length(sbc);
                //初始化帧计数
                sbc.priv.frame.frame_count = 1;
                sbc.priv.init = true;

            } else if (sbc.priv.frame.bitpool != sbc.bitpool) {
                sbc.priv.frame.codesize = (ushort)codesize;
                sbc.bitpool = sbc.priv.frame.bitpool;
            }            

            //初始话witten
            if(!Convert.IsDBNull(written))
                written = 0;

            if (codesize <= 0)
                return codesize;

            //polyphase synthesis
            //多相分析参数表：
            //@para priv.dec_state编码器的     priv.frame-sbc编码后的帧数据

            //经过量化解析之后才会送到这个位置
            samples = sbc_synthesize_audio(sbc.priv.dec_state, sbc.priv.frame);


            if (output_len < samples * sbc.priv.frame.channels * 2)
                samples = output_len / (sbc.priv.frame.channels * 2);

            for (i = 0; i < samples; i++) {
                for (ch = 0; ch < sbc.priv.frame.channels; ch++) {
                    short s;
                    s = sbc.priv.frame.pcm_sample[ch,i];
                    int index = (i * sbc.priv.frame.channels + ch) * 2;

                    if (sbc.endian == Constants.SBC_LE) {
                        output[index] = (byte)(s & 0x00ff);
                        output[index+1] = (byte)((s & 0xff00) >> 8);
                    } else {
                        output[index] = (byte)((s & 0xff00) >> 8);
                        output[index+1] = (byte)(s & 0x00ff);
                    }
                }
            }
     
            //计算
            if(!Convert.IsDBNull(written))
                written = samples * sbc.priv.frame.channels * 2;
    

            return codesize;
        }


        void sbc_decoder_init(sbc_decoder_state state, sbc_frame frame)
        {
            System.Diagnostics.Debug.WriteLine("sbc_decoder_init()!");
            int i, ch;
            //set 0 for all elements of V[,]
            //数组V初始化全0
            //set subbands
            state.subbands = frame.subbands;

            //一共两个传输通道
            for (ch = 0; ch < 2; ch++)
                for (i = 0; i < frame.subbands * 2; i++)
                    //ch_num * subbands_num * 2
                    state.offset[ch,i] = (10 * i + 10);
        }


        public int sbc_unpack_frame(byte[] data, sbc_frame frame, int len)
        {
            System.Diagnostics.Debug.WriteLine("sbc_unpack_frame()!");
            int consumed;
            byte[] crc_header = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            int crc_pos = 0;
            int temp;

            int audio_sample;
            int ch, sb, blk, bit;

            int[,] bits = new int[2,8];
            uint[,] levels = new uint[2,8];

            if (len < 4)
                return -1;

            if (data[0] != Constants.SBC_SYNCWORD)
                return -2;

            frame.frequency = (byte)((data[1] >> 6) & 0x03);

            frame.block_mode = (byte)((data[1] >> 4) & 0x03);
            switch (frame.block_mode) {
            case Constants.SBC_BLK_4:
                frame.blocks = 4;
                break;
            case Constants.SBC_BLK_8:
                frame.blocks = 8;
                break;
            case Constants.SBC_BLK_12:
                frame.blocks = 12;
                break;
            case Constants.SBC_BLK_16:
                frame.blocks = 16;
                break;
            }

            frame.mode = (Channels)((data[1] >> 2) & 0x03); //可能存在问题
            switch (frame.mode) {
            case Channels.MONO:
                frame.channels = 1;
                break;
            case Channels.DUAL_CHANNEL:	/* fall-through */
            case Channels.STEREO:
            case Channels.JOINT_STEREO:
                frame.channels = 2;
                break;
            }

            frame.allocation = (Allocate)((data[1] >> 1) & 0x01);

            frame.subband_mode = (byte)(data[1] & 0x01);
            frame.subbands = (byte)(frame.subband_mode == Constants.SBC_SB_8 ? 8 : 4);

            frame.bitpool = data[2];

            if ((frame.mode == Channels.MONO || frame.mode == Channels.DUAL_CHANNEL) &&
                    frame.bitpool > 16 * frame.subbands)
                return -4;

            if ((frame.mode == Channels.STEREO || frame.mode == Channels.JOINT_STEREO) &&
                    frame.bitpool > 32 * frame.subbands)
                return -4;

            /* data[3] is crc, we're checking it later */

            consumed = 32;

            crc_header[0] = data[1];
            crc_header[1] = data[2];
            crc_pos = 16;

            if (frame.mode == Channels.JOINT_STEREO) {
                if (len * 8 < (uint)(consumed + frame.subbands))
                    return -1;

                frame.joint = 0x00;
                for (sb = 0; sb < frame.subbands - 1; sb++)
                    frame.joint |= (byte)(((data[4] >> (7 - sb)) & 0x01) << sb);
                if (frame.subbands == 4)
                    crc_header[crc_pos / 8] = (byte)(data[4] & 0xf0);
                else
                    crc_header[crc_pos / 8] = data[4];

                consumed += frame.subbands;
                crc_pos += frame.subbands;
            }

            if (len * 8 < (byte)(consumed + (4 * frame.subbands * frame.channels)))
                return -1;

            for (ch = 0; ch < frame.channels; ch++) {
                for (sb = 0; sb < frame.subbands; sb++) {
                    /* FIXME assert(consumed % 4 == 0); */
                    frame.scale_factor[ch,sb] =
                        (uint)(data[consumed >> 3] >> (4 - (consumed & 0x7))) & 0x0F;
                    crc_header[crc_pos >> 3] |=(byte)(
                        frame.scale_factor[ch,sb] << (4 - (crc_pos & (0x7))));

                    consumed += 4;
                    crc_pos += 4;
                }
            }

            if (data[3] != exp.sbc_crc8(crc_header, crc_pos))
                return -3;

            sbc_calculate_bits(frame, bits);

            for (ch = 0; ch < frame.channels; ch++) {
                for (sb = 0; sb < frame.subbands; sb++)
                    levels[ch,sb] = (uint)((1 << bits[ch,sb]) - 1);
            }

            for (blk = 0; blk < frame.blocks; blk++) {
                for (ch = 0; ch < frame.channels; ch++) {
                    for (sb = 0; sb < frame.subbands; sb++) {
                        if (levels[ch,sb] > 0) {
                            audio_sample = 0;
                            for (bit = 0; bit < bits[ch,sb]; bit++) {
                                if (consumed > len * 8)
                                    return -1;

                                if (((data[consumed >> 3] >> (7 - (consumed & 0x7))) & 0x01) != 0)
                                    audio_sample |= 1 << (bits[ch,sb] - bit - 1);

                                consumed++;
                            }

                            frame.sb_sample[blk,ch,sb] =
                                (int)((((audio_sample << 1) | 1) << ((int)frame.scale_factor[ch,sb] + 1)) /
                                levels[ch,sb] - (1 << (int)(frame.scale_factor[ch,sb] + 1)));
                        } else
                            frame.sb_sample[blk,ch,sb] = 0;
                    }
                }
            }

            if (frame.mode == Channels.JOINT_STEREO) {
                for (blk = 0; blk < frame.blocks; blk++) {
                    for (sb = 0; sb < frame.subbands; sb++) {
                        if ((frame.joint & (0x01 << sb)) != 0) {
                            temp = frame.sb_sample[blk,0,sb] +
                                frame.sb_sample[blk,1,sb];
                            frame.sb_sample[blk,1,sb] =
                                frame.sb_sample[blk,0,sb] -
                                frame.sb_sample[blk,1,sb];
                            frame.sb_sample[blk,0,sb] = temp;
                        }
                    }
                }
            }

            if ((consumed & 0x7) != 0)
                consumed += 8 - (consumed & 0x7);

            return consumed >> 3;            

        
        }

        void sbc_calculate_bits(sbc_frame frame, int[,] bits)
        {
            System.Diagnostics.Debug.WriteLine("sbc_calculate_bits()!");
            if (frame.subbands == 4)
                sbc_calculate_bits_internal(frame, bits, 4);
            else
                sbc_calculate_bits_internal(frame, bits, 8);
        }

        void sbc_calculate_bits_internal(sbc_frame frame, int[,] bits,int subbands)
        {
            System.Diagnostics.Debug.WriteLine("sbc_calculate_bits_internal()!");
            byte sf = frame.frequency;

            if (frame.mode == Channels.MONO || frame.mode == Channels.DUAL_CHANNEL) {
                int[,] bitneed = new int[2,8];
                int loudness, max_bitneed, bitcount, slicecount, bitslice;
                int ch, sb;

                for (ch = 0; ch < frame.channels; ch++) {
                    max_bitneed = 0;
                    if (frame.allocation == Allocate.SNR) {
                        for (sb = 0; sb < subbands; sb++) {
                            bitneed[ch,sb] = (int)(frame.scale_factor[ch,sb]);
                            if (bitneed[ch,sb] > max_bitneed)
                                max_bitneed = bitneed[ch,sb];
                        }
                    } else {
                        for (sb = 0; sb < subbands; sb++) {
                            if (frame.scale_factor[ch,sb] == 0)
                                bitneed[ch,sb] = -5;
                            else {
                                if (subbands == 4)
                                    loudness = (int)(frame.scale_factor[ch,sb] - SBCProtcol.sbc_offset4[sf,sb]);
                                else
                                    loudness = (int)(frame.scale_factor[ch,sb] - SBCProtcol.sbc_offset8[sf,sb]);
                                if (loudness > 0)
                                    bitneed[ch,sb] = loudness / 2;
                                else
                                    bitneed[ch,sb] = loudness;
                            }
                            if (bitneed[ch,sb] > max_bitneed)
                                max_bitneed = bitneed[ch,sb];
                        }
                    }

                    bitcount = 0;
                    slicecount = 0;
                    bitslice = max_bitneed + 1;
                    do {
                        bitslice--;
                        bitcount += slicecount;
                        slicecount = 0;
                        for (sb = 0; sb < subbands; sb++) {
                            if ((bitneed[ch,sb] > bitslice + 1) && (bitneed[ch,sb] < bitslice + 16))
                                slicecount++;
                            else if (bitneed[ch,sb] == bitslice + 1)
                                slicecount += 2;
                        }
                    } while (bitcount + slicecount < frame.bitpool);

                    if (bitcount + slicecount == frame.bitpool) {
                        bitcount += slicecount;
                        bitslice--;
                    }

                    for (sb = 0; sb < subbands; sb++) {
                        if (bitneed[ch,sb] < bitslice + 2)
                            bits[ch,sb] = 0;
                        else {
                            bits[ch,sb] = bitneed[ch,sb] - bitslice;
                            if (bits[ch,sb] > 16)
                                bits[ch,sb] = 16;
                        }
                    }

                    for (sb = 0; bitcount < frame.bitpool &&
                                    sb < subbands; sb++) {
                        if ((bits[ch,sb] >= 2) && (bits[ch,sb] < 16)) {
                            bits[ch,sb]++;
                            bitcount++;
                        } else if ((bitneed[ch,sb] == bitslice + 1) && (frame.bitpool > bitcount + 1)) {
                            bits[ch,sb] = 2;
                            bitcount += 2;
                        }
                    }

                    for (sb = 0; bitcount < frame.bitpool &&
                                    sb < subbands; sb++) {
                        if (bits[ch,sb] < 16) {
                            bits[ch,sb]++;
                            bitcount++;
                        }
                    }

                }

            } else if (frame.mode == Channels.STEREO || frame.mode == Channels.JOINT_STEREO) {
                int[,] bitneed = new int[2,8];
                int loudness, max_bitneed, bitcount, slicecount, bitslice;
                int ch, sb;

                max_bitneed = 0;
                if (frame.allocation == Allocate.SNR) {
                    for (ch = 0; ch < 2; ch++) {
                        for (sb = 0; sb < subbands; sb++) {
                            bitneed[ch,sb] = (int)(frame.scale_factor[ch,sb]);
                            if (bitneed[ch,sb] > max_bitneed)
                                max_bitneed = bitneed[ch,sb];
                        }
                    }
                } else {
                    for (ch = 0; ch < 2; ch++) {
                        for (sb = 0; sb < subbands; sb++) {
                            if (frame.scale_factor[ch,sb] == 0)
                                bitneed[ch,sb] = -5;
                            else {
                                if (subbands == 4)
                                    loudness = (int)(frame.scale_factor[ch,sb] - SBCProtcol.sbc_offset4[sf,sb]);
                                else
                                    loudness = (int)(frame.scale_factor[ch,sb] - SBCProtcol.sbc_offset8[sf,sb]);
                                if (loudness > 0)
                                    bitneed[ch,sb] = loudness / 2;
                                else
                                    bitneed[ch,sb] = loudness;
                            }
                            if (bitneed[ch,sb] > max_bitneed)
                                max_bitneed = bitneed[ch,sb];
                        }
                    }
                }

                bitcount = 0;
                slicecount = 0;
                bitslice = max_bitneed + 1;
                do {
                    bitslice--;
                    bitcount += slicecount;
                    slicecount = 0;
                    for (ch = 0; ch < 2; ch++) {
                        for (sb = 0; sb < subbands; sb++) {
                            if ((bitneed[ch,sb] > bitslice + 1) && (bitneed[ch,sb] < bitslice + 16))
                                slicecount++;
                            else if (bitneed[ch,sb] == bitslice + 1)
                                slicecount += 2;
                        }
                    }
                } while (bitcount + slicecount < frame.bitpool);

                if (bitcount + slicecount == frame.bitpool) {
                    bitcount += slicecount;
                    bitslice--;
                }

                for (ch = 0; ch < 2; ch++) {
                    for (sb = 0; sb < subbands; sb++) {
                        if (bitneed[ch,sb] < bitslice + 2) {
                            bits[ch,sb] = 0;
                        } else {
                            bits[ch,sb] = bitneed[ch,sb] - bitslice;
                            if (bits[ch,sb] > 16)
                                bits[ch,sb] = 16;
                        }
                    }
                }

                ch = 0;
                sb = 0;
                while (bitcount < frame.bitpool) {
                    if ((bits[ch,sb] >= 2) && (bits[ch,sb] < 16)) {
                        bits[ch,sb]++;
                        bitcount++;
                    } else if ((bitneed[ch,sb] == bitslice + 1) && (frame.bitpool > bitcount + 1)) {
                        bits[ch,sb] = 2;
                        bitcount += 2;
                    }
                    if (ch == 1) {
                        ch = 0;
                        sb++;
                        if (sb >= subbands)
                            break;
                    } else
                        ch = 1;
                }

                ch = 0;
                sb = 0;
                while (bitcount < frame.bitpool) {
                    if (bits[ch,sb] < 16) {
                        bits[ch,sb]++;
                        bitcount++;
                    }
                    if (ch == 1) {
                        ch = 0;
                        sb++;
                        if (sb >= subbands)
                            break;
                    } else
                        ch = 1;
                }

            }

        }
        
        ushort sbc_get_dec_codesize(sbc_struct sbc)
        {
            System.Diagnostics.Debug.WriteLine("sbc_get_dec_codesize()!");
            int ret;
            byte subbands, channels, blocks, joint, bitpool;
 
            //若完成初始化，则相关的参数都存在sbc.priv.frame对应的结构体中
            if (sbc.priv.init && sbc.priv.frame.bitpool == sbc.bitpool)
                return sbc.priv.frame.codesize;

            subbands = (byte)(sbc.subbands == Constants.SBC_SB_8 ? 8 : 4);
            blocks = (byte)(4 + (sbc.blocks * 4));
            channels = (byte)(sbc.mode == Constants.SBC_MODE_MONO ? 1 : 2);
            joint = (byte)(sbc.mode == Constants.SBC_MODE_JOINT_STEREO ? 1 : 0);
            bitpool = sbc.bitpool;

            ret = 4 + (4 * subbands * channels) / 8;
            /* This term is not always evenly divide so we round it up */
            if (channels == 1 || sbc.mode == Constants.SBC_MODE_DUAL_CHANNEL)
                //如果是MONO或者DUAL_CHANNEL
                ret += ((blocks * channels * bitpool) + 7) / 8;
            else
                //如果是STEREO或者JOINT STEREO
                ret += (((joint == 0 ?0 : subbands) + blocks * bitpool) + 7) / 8;

            return (ushort)ret;
        }


        ushort sbc_get_dec_frame_length(sbc_struct sbc)
        {
            System.Diagnostics.Debug.WriteLine("sbc_get_dec_frame_length()!");
            ushort subbands, channels, blocks;
            //若未初始化
            if (!sbc.priv.init) {
                subbands = (ushort)(sbc.subbands == Constants.SBC_SB_8 ? 8 : 4);
                blocks = (ushort)(4 + (sbc.blocks * 4));
                channels = (ushort)(sbc.mode == Constants.SBC_MODE_MONO ? 1 : 2);
            } else {
                //已完成初始化
                subbands = sbc.priv.frame.subbands;
                blocks = sbc.priv.frame.blocks;
                channels = sbc.priv.frame.channels;
            }

            return (ushort)(subbands * blocks * channels * 2);
        }


        int sbc_synthesize_audio(sbc_decoder_state state, sbc_frame frame)
        {
            System.Diagnostics.Debug.WriteLine("sbc_synthesize_audio()!");
            int ch, blk;

            switch (sbc.priv.frame.subbands) {
            case 4:
                for (ch = 0; ch < sbc.priv.frame.channels; ch++) {
                    for (blk = 0; blk < sbc.priv.frame.blocks; blk++)
                        sbc_synthesize_four(state, frame, ch, blk);
                }
                return sbc.priv.frame.blocks * 4;

            case 8:
                for (ch = 0; ch < sbc.priv.frame.channels; ch++) {
                    for (blk = 0; blk < sbc.priv.frame.blocks; blk++)
                        sbc_synthesize_eight(state, frame, ch, blk);
                }
                return sbc.priv.frame.blocks * 8;

            default:
                return -5;
            }
        }

        void sbc_synthesize_four(sbc_decoder_state state, sbc_frame frame, int ch, int blk)
        {
            System.Diagnostics.Debug.WriteLine("sbc_decode()!");
            int i, k, idx;

            //获取单通道的V值
            int[] v = new int[state.V.Rank];
            for(i = 0; i < state.V.Rank; i++)
                v[i] = state.V[ch,i];

            //获取单通道的offset值
            int[] offset = new int[state.offset.Rank];
            for(i = 0; i < state.offset.Rank; i++)
                offset[i] = state.offset[ch,i];  


            //Matrixing
            //for k=0 to 7 do
            // for i=0 to 3 do 
            for (i = 0; i < 8; i++) {
                /* Shifting */
                offset[i]--;
                if (offset[i] < 0) {
                    offset[i] = 79;
                    //memcpy(v + 80, v, 9 * sizeof(*v));
                    for(int j = 0; i < 9 * sizeof(int); j++)
                        v[80 + j] = v[j];
                }

                /* Distribute the new matrix value to the shifted position */
                //synmatix[k,i] is N[k,i]
                v[offset[i]] = exp.SCALE4_STAGED1(
                    exp.MULA(SBCProtcol.synmatrix4[i,0], frame.sb_sample[blk,ch,0],
                    exp.MULA(SBCProtcol.synmatrix4[i,1], frame.sb_sample[blk,ch,1],
                    exp.MULA(SBCProtcol.synmatrix4[i,2], frame.sb_sample[blk,ch,2],
                    exp.MUL (SBCProtcol.synmatrix4[i,3], frame.sb_sample[blk,ch,3])))));
            }

            /* Compute the samples */
            for (idx = 0, i = 0; i < 4; i++, idx += 5) {
                k = (i + 4) & 0xf;

                /* Store in output, Q0 */
                //括号内的结果最终右移15位（>>15）
                //角标计算方法
                //frame.pcm_sample[ch,blk * 4 + i] = sbc_clip16(SCALE4_STAGED1(
                frame.pcm_sample[ch,blk * 4 + i] = (short)exp.SCALE4_STAGED2(
                    exp.MULA(v[offset[i] + 0], SBCProtcol.sbc_proto_4_40m0[idx + 0],//每两个为一组
                    exp.MULA(v[offset[k] + 1], SBCProtcol.sbc_proto_4_40m1[idx + 0],//
                    exp.MULA(v[offset[i] + 2], SBCProtcol.sbc_proto_4_40m0[idx + 1],
                    exp.MULA(v[offset[k] + 3], SBCProtcol.sbc_proto_4_40m1[idx + 1],
                    exp.MULA(v[offset[i] + 4], SBCProtcol.sbc_proto_4_40m0[idx + 2],
                    exp.MULA(v[offset[k] + 5], SBCProtcol.sbc_proto_4_40m1[idx + 2],
                    exp.MULA(v[offset[i] + 6], SBCProtcol.sbc_proto_4_40m0[idx + 3],
                    exp.MULA(v[offset[k] + 7], SBCProtcol.sbc_proto_4_40m1[idx + 3],
                    exp.MULA(v[offset[i] + 8], SBCProtcol.sbc_proto_4_40m0[idx + 4],
                    exp.MUL( v[offset[k] + 9], SBCProtcol.sbc_proto_4_40m1[idx + 4])))))))))));
            }
        }

        void sbc_synthesize_eight(sbc_decoder_state state, sbc_frame frame, int ch, int blk)
        {
            System.Diagnostics.Debug.WriteLine("sbc_synthesize_eight()!");
            int i, j, k, idx;
            int[] offset = new int[state.offset.Rank];
            for(i = 0; i < state.offset.Rank; i++)
                offset[i] = state.offset[ch,i];


            for (i = 0; i < 16; i++) {
                /* Shifting */
                offset[i]--;
                if (offset[i] < 0) {
                    offset[i] = 159;
                }
                }
                        state.V[ch, 160] = state.V[ch,0];
                        state.V[ch, 161] = state.V[ch,1];
                        state.V[ch, 162] = state.V[ch,2];
                        state.V[ch, 163] = state.V[ch,3];
                        state.V[ch, 164] = state.V[ch,4];
                        state.V[ch, 165] = state.V[ch,5];
                        state.V[ch, 166] = state.V[ch,6];
                        state.V[ch, 167] = state.V[ch,7];
                        state.V[ch, 168] = state.V[ch,0];
                
            //	 Distribute the new matrix value to the shifted position 
            //	  it is too wast of time
        /*			state.V[ch,offset[i]] = exp.SCALE8_STAGED1(
                    exp.MULA(SBCProtcol.synmatrix8[i,0], frame.sb_sample[blk,ch,0],
                    exp.MULA(SBCProtcol.synmatrix8[i,1], frame.sb_sample[blk,ch,1],
                    exp.MULA(SBCProtcol.synmatrix8[i,2], frame.sb_sample[blk,ch,2],
                    exp.MULA(SBCProtcol.synmatrix8[i,3], frame.sb_sample[blk,ch,3],
                    exp.MULA(SBCProtcol.synmatrix8[i,4], frame.sb_sample[blk,ch,4],
                    exp.MULA(SBCProtcol.synmatrix8[i,5], frame.sb_sample[blk,ch,5],
                    exp.MULA(SBCProtcol.synmatrix8[i,6], frame.sb_sample[blk,ch,6],
                    exp.MUL( SBCProtcol.synmatrix8[i,7], frame.sb_sample[blk,ch,7])))))))));
                    
            */
        /*	yy.........  09.1.6 for optimization 	*/	
                int[] x = new int[8];

                        x[0] = frame.sb_sample[blk,ch,0];
                        x[1] = frame.sb_sample[blk,ch,1];
                        x[2] = frame.sb_sample[blk,ch,2];
                        x[3] = frame.sb_sample[blk,ch,3];
                        x[4] = frame.sb_sample[blk,ch,4];
                        x[5] = frame.sb_sample[blk,ch,5];
                        x[6] = frame.sb_sample[blk,ch,6];
                        x[7] = frame.sb_sample[blk,ch,7];
                    
                int[] s = new int[7];									
                    
                    s[0] = (x[0] + x[3]) + (x[4] + x[7]) - ((x[1] + x[2]) + (x[5] + x[6]));
                    
                    s[1] = x[0] - x[7];
                    s[2] = x[1] - x[6];
                    s[3] = x[2] - x[5];
                    s[4] = x[3] - x[4];
                    
                    s[5] = (x[0] + x[7]) - (x[3] + x[4]);
                    s[6] = (x[1] + x[6]) - (x[2] + x[5]);
                    

                    s[7] = (x[0] + x[1]) + (x[2] + x[3]) + (x[4] + x[5]) + (x[6] + x[7]);

                    state.V[ch,offset[0]] = exp.SCALE8_STAGED1(exp.MUL(SBCProtcol.synmatrix8[0],s[0]));
                                
                        state.V[ch,offset[1]] = exp.SCALE8_STAGED1(
                        exp.MULA(SBCProtcol.synmatrix8[1],s[1],
                        exp.MULA(-SBCProtcol.synmatrix8[2],s[2],
                        exp.MULA(SBCProtcol.synmatrix8[3],s[3],
                        exp.MUL(SBCProtcol.synmatrix8[4],s[4])))));
                        
                        state.V[ch,offset[2]] = exp.SCALE8_STAGED1(
                        exp.MULA(SBCProtcol.synmatrix8[5],s[5],
                        exp.MUL(-SBCProtcol.synmatrix8[6],s[6])));

                        state.V[ch,offset[3]] = exp.SCALE8_STAGED1(
                        exp.MULA(SBCProtcol.synmatrix8[3],s[1],
                        exp.MULA(-SBCProtcol.synmatrix8[1],s[2],
                        exp.MULA(SBCProtcol.synmatrix8[4],s[3],
                        exp.MUL(-SBCProtcol.synmatrix8[2],s[4])))));
                
                        state.V[ch,offset[4]] = 0;

                        state.V[ch,offset[5]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[3],s[1],
                        exp.MULA(SBCProtcol.synmatrix8[1],s[2],
                        exp.MULA(-SBCProtcol.synmatrix8[4],s[3],
                        exp.MUL(SBCProtcol.synmatrix8[2],s[4])))));

                        state.V[ch,offset[6]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[5],s[5],
                        exp.MUL(SBCProtcol.synmatrix8[6],s[6])));
                        
                        state.V[ch,offset[7]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[1],s[1],
                        exp.MULA(SBCProtcol.synmatrix8[2],s[2],
                        exp.MULA(-SBCProtcol.synmatrix8[3],s[3],
                        exp.MUL(-SBCProtcol.synmatrix8[4],s[4])))));

                        state.V[ch,offset[8]] = exp.SCALE8_STAGED1(exp.MUL(-SBCProtcol.synmatrix8[0],s[0]));

                        state.V[ch,offset[9]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[4],s[1],
                        exp.MULA(SBCProtcol.synmatrix8[3],s[2],
                        exp.MULA(SBCProtcol.synmatrix8[2],s[3],
                        exp.MUL(SBCProtcol.synmatrix8[1],s[4])))));

                        state.V[ch,offset[10]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[6],s[5],
                        exp.MUL(-SBCProtcol.synmatrix8[5],s[6])));

                        state.V[ch,offset[11]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[2],s[1],
                        exp.MULA(-SBCProtcol.synmatrix8[4],s[2],
                        exp.MULA(-SBCProtcol.synmatrix8[1],s[3],
                        exp.MUL(-SBCProtcol.synmatrix8[3],s[4])))));

                        state.V[ch,offset[12]] = exp.SCALE8_STAGED1(exp.MUL(SBCProtcol.synmatrix8[7],s[7]));

                        state.V[ch,offset[13]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[2],s[1],
                        exp.MULA(-SBCProtcol.synmatrix8[4],s[2],
                        exp.MULA(-SBCProtcol.synmatrix8[1],s[3],
                        exp.MUL(-SBCProtcol.synmatrix8[3],s[4])))));

                        state.V[ch,offset[14]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[6],s[5],
                        exp.MUL(-SBCProtcol.synmatrix8[5],s[6])));

                        state.V[ch,offset[15]] = exp.SCALE8_STAGED1(
                        exp.MULA(-SBCProtcol.synmatrix8[4],s[1],
                        exp.MULA(SBCProtcol.synmatrix8[3],s[2],
                        exp.MULA(SBCProtcol.synmatrix8[2],s[3],
                        exp.MUL(SBCProtcol.synmatrix8[1],s[4])))));

            /* Compute the samples */
            for (idx = 0, i = 0; i < 8; i++, idx += 5) {
                k = (i + 8) & 0xf;

                /* Store in output, Q0 */
                frame.pcm_sample[ch,blk * 8 + i] = exp.sbc_clip16(exp.SCALE8_STAGED1(
                    exp.MULA(state.V[ch,offset[i] + 0], SBCProtcol.sbc_proto_8_80m0[idx + 0],
                    exp.MULA(state.V[ch,offset[k] + 1], SBCProtcol.sbc_proto_8_80m1[idx + 0],
                    exp.MULA(state.V[ch,offset[i] + 2], SBCProtcol.sbc_proto_8_80m0[idx + 1],
                    exp.MULA(state.V[ch,offset[k] + 3], SBCProtcol.sbc_proto_8_80m1[idx + 1],
                    exp.MULA(state.V[ch,offset[i] + 4], SBCProtcol.sbc_proto_8_80m0[idx + 2],
                    exp.MULA(state.V[ch,offset[k] + 5], SBCProtcol.sbc_proto_8_80m1[idx + 2],
                    exp.MULA(state.V[ch,offset[i] + 6], SBCProtcol.sbc_proto_8_80m0[idx + 3],
                    exp.MULA(state.V[ch,offset[k] + 7], SBCProtcol.sbc_proto_8_80m1[idx + 3],
                    exp.MULA(state.V[ch,offset[i] + 8], SBCProtcol.sbc_proto_8_80m0[idx + 4],
                    exp.MUL( state.V[ch,offset[k] + 9], SBCProtcol.sbc_proto_8_80m1[idx + 4]))))))))))));
            }
        }


        public void Decode(byte data)
        {
            inputStream[Readindex++] = data;
            if  (Readindex >= inputSize)
            {
                Readindex = 0;
                WriteIndex +=sbc_decode(inputStream, inputSize, 
                                            outputStream, outputSize, decoded);
                if(WriteIndex >= 5 * inputSize)
                {
                    SBCOutput.Invoke(this,outputStream);
                    WriteIndex = 0;
                }
            }         
        }

        //与ADPCM解码不同
        //数据需要达到一定长度之后才能进行解码
        public void Decode(byte[] data)
        {
            
            foreach(var x in data) Decode(x);
        }
     
    }
}