using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace INGdemo.Lib
{
    public class SBCProtcol
    {

        public static int[,] sbc_offset4 = {
            { -1, 0, 0, 0 },
	        { -2, 0, 0, 1 },
	        { -2, 0, 0, 1 },
	        { -2, 0, 0, 1 }
        };

        public static int[,] sbc_offset8 = {
            { -2, 0, 0, 0, 0, 0, 0, 1 },
            { -3, 0, 0, 0, 0, 0, 1, 2 },
            { -4, 0, 0, 0, 0, 0, 1, 2 },
            { -4, 0, 0, 0, 0, 0, 1, 2 }
        };
        public static int[] sbc_proto_4_40m0 = {
            exp.SS4(0x00000000),            exp.SS4(exp.CI(0xffa6982f)),    exp.SS4(exp.CI(0xfba93848)),    exp.SS4(0x0456c7b8),
	        exp.SS4(0x005967d1),            exp.SS4(exp.CI(0xfffb9ac7)),    exp.SS4(exp.CI(0xff589157)),    exp.SS4(exp.CI(0xf9c2a8d8)),
	        exp.SS4(0x027c1434),            exp.SS4(0x0019118b),            exp.SS4(exp.CI(0xfff3c74c)),    exp.SS4(exp.CI(0xff137330)),
	        exp.SS4(exp.CI(0xf81b8d70)),    exp.SS4(0x00ec1b8b),            exp.SS4(exp.CI(0xfff0b71a)),    exp.SS4(exp.CI(0xffe99b00)),
	        exp.SS4(exp.CI(0xfef84470)),    exp.SS4(exp.CI(0xf6fb4370)),    exp.SS4(exp.CI(0xffcdc351)),    exp.SS4(exp.CI(0xffe01dc7))
        };

        public static int[] sbc_proto_4_40m1 = {
        	exp.SS4(exp.CI(0xffe090ce)),    exp.SS4(exp.CI(0xff2c0475)),    exp.SS4(exp.CI(0xf694f800)),    exp.SS4(exp.CI(0xff2c0475)),
	        exp.SS4(exp.CI(0xffe090ce)),    exp.SS4(exp.CI(0xffe01dc7)),    exp.SS4(exp.CI(0xffcdc351)),    exp.SS4(exp.CI(0xf6fb4370)),
	        exp.SS4(exp.CI(0xfef84470)),    exp.SS4(exp.CI(0xffe99b00)),    exp.SS4(exp.CI(0xfff0b71a)),    exp.SS4(0x00ec1b8b),
	        exp.SS4(exp.CI(0xf81b8d70)),    exp.SS4(exp.CI(0xff137330)),    exp.SS4(exp.CI(0xfff3c74c)),    exp.SS4(0x0019118b),
	        exp.SS4(0x027c1434),            exp.SS4(exp.CI(0xf9c2a8d8)),    exp.SS4(exp.CI(0xff589157)),    exp.SS4(exp.CI(0xfffb9ac7))
        };

        public static int[] sbc_proto_8_80m0 = {
            exp.SS8(0x00000000),            exp.SS8(exp.CI(0xfe8d1970)),    exp.SS8(exp.CI(0xee979f00)),    exp.SS8(0x11686100),
            exp.SS8(0x0172e690),            exp.SS8(exp.CI(0xfff5bd1a)),    exp.SS8(exp.CI(0xfdf1c8d4)),    exp.SS8(exp.CI(0xeac182c0)),
            exp.SS8(0x0d9daee0),            exp.SS8(0x00e530da),            exp.SS8(exp.CI(0xffe9811d)),    exp.SS8(exp.CI(0xfd52986c)),
            exp.SS8(exp.CI(0xe7054ca0)),    exp.SS8(0x0a00d410),            exp.SS8(0x006c1de4),            exp.SS8(exp.CI(0xffdba705)),
            exp.SS8(exp.CI(0xfcbc98e8)),    exp.SS8(exp.CI(0xe3889d20)),    exp.SS8(0x06af2308),            exp.SS8(0x000bb7db),
            exp.SS8(exp.CI(0xffca00ed)),    exp.SS8(exp.CI(0xfc3fbb68)),    exp.SS8(exp.CI(0xe071bc00)),    exp.SS8(0x03bf7948),
            exp.SS8(exp.CI(0xffc4e05c)),    exp.SS8(exp.CI(0xffb54b3b)),    exp.SS8(exp.CI(0xfbedadc0)),    exp.SS8(exp.CI(0xdde26200)),
            exp.SS8(0x0142291c),            exp.SS8(exp.CI(0xff960e94)),    exp.SS8(exp.CI(0xff9f3e17)),    exp.SS8(exp.CI(0xfbd8f358)),
            exp.SS8(exp.CI(0xdbf79400)),    exp.SS8(exp.CI(0xff405e01)),    exp.SS8(exp.CI(0xff7d4914)),    exp.SS8(exp.CI(0xff8b1a31)),
            exp.SS8(exp.CI(0xfc1417b8)),    exp.SS8(exp.CI(0xdac7bb40)),    exp.SS8(exp.CI(0xfdbb828c)),    exp.SS8(exp.CI(0xff762170))
        };

        public static int[] sbc_proto_8_80m1 = {
            exp.SS8(exp.CI(0xff7c272c)),    exp.SS8(exp.CI(0xfcb02620)),    exp.SS8(exp.CI(0xda612700)),    exp.SS8(exp.CI(0xfcb02620)),
            exp.SS8(exp.CI(0xff7c272c)),    exp.SS8(exp.CI(0xff762170)),    exp.SS8(exp.CI(0xfdbb828c)),    exp.SS8(exp.CI(0xdac7bb40)),
            exp.SS8(exp.CI(0xfc1417b8)),    exp.SS8(exp.CI(0xff8b1a31)),    exp.SS8(exp.CI(0xff7d4914)),    exp.SS8(exp.CI(0xff405e01)),
            exp.SS8(exp.CI(0xdbf79400)),    exp.SS8(exp.CI(0xfbd8f358)),    exp.SS8(exp.CI(0xff9f3e17)),    exp.SS8(exp.CI(0xff960e94)),
            exp.SS8(0x0142291c),            exp.SS8(exp.CI(0xdde26200)),    exp.SS8(exp.CI(0xfbedadc0)),    exp.SS8(exp.CI(0xffb54b3b)),
            exp.SS8(exp.CI(0xffc4e05c)),    exp.SS8(0x03bf7948),            exp.SS8(exp.CI(0xe071bc00)),    exp.SS8(exp.CI(0xfc3fbb68)),
            exp.SS8(exp.CI(0xffca00ed)),    exp.SS8(0x000bb7db),            exp.SS8(0x06af2308),            exp.SS8(exp.CI(0xe3889d20)),
            exp.SS8(exp.CI(0xfcbc98e8)),    exp.SS8(exp.CI(0xffdba705)),    exp.SS8(0x006c1de4),            exp.SS8(0x0a00d410),
            exp.SS8(exp.CI(0xe7054ca0)),    exp.SS8(exp.CI(0xfd52986c)),    exp.SS8(exp.CI(0xffe9811d)),    exp.SS8(0x00e530da),
            exp.SS8(0x0d9daee0),            exp.SS8(exp.CI(0xeac182c0)),    exp.SS8(exp.CI(0xfdf1c8d4)),    exp.SS8(exp.CI(0xfff5bd1a))
        };

        public static int[,] synmatrix4 = {
            { exp.SN4(0x05a82798),          exp.SN4(exp.CI(0xfa57d868)),    exp.SN4(exp.CI(0xfa57d868)),    exp.SN4(0x05a82798) },
	        { exp.SN4(0x030fbc54),          exp.SN4(exp.CI(0xf89be510)),    exp.SN4(0x07641af0),            exp.SN4(exp.CI(0xfcf043ac)) },
	        { exp.SN4(0x00000000),          exp.SN4(0x00000000),            exp.SN4(0x00000000),            exp.SN4(0x00000000) },
	        { exp.SN4(exp.CI(0xfcf043ac)),  exp.SN4(0x07641af0),            exp.SN4(exp.CI(0xf89be510)),    exp.SN4(0x030fbc54) },
	        { exp.SN4(exp.CI(0xfa57d868)),  exp.SN4(0x05a82798),            exp.SN4(0x05a82798),            exp.SN4(exp.CI(0xfa57d868)) },
	        { exp.SN4(exp.CI(0xf89be510)),  exp.SN4(exp.CI(0xfcf043ac)),    exp.SN4(0x030fbc54),            exp.SN4(0x07641af0) },
	        { exp.SN4(exp.CI(0xf8000000)),  exp.SN4(exp.CI(0xf8000000)),    exp.SN4(exp.CI(0xf8000000)),    exp.SN4(exp.CI(0xf8000000)) },
	        { exp.SN4(exp.CI(0xf89be510)),  exp.SN4(exp.CI(0xfcf043ac)),    exp.SN4(0x030fbc54),            exp.SN4(0x07641af0) }
        };

        public static int[,] synmatrix8 = {
            { exp.SN8(0x05a82798), exp.SN8(0xfa57d868), exp.SN8(0xfa57d868), exp.SN8(0x05a82798), exp.SN8(0x05a82798), exp.SN8(0xfa57d868), exp.SN8(0xfa57d868), exp.SN8(0x05a82798) },
            { exp.SN8(0x0471ced0), exp.SN8(0xf8275a10), exp.SN8(0x018f8b84), exp.SN8(0x06a6d988), exp.SN8(0xf9592678), exp.SN8(0xfe70747c), exp.SN8(0x07d8a5f0), exp.SN8(0xfb8e3130) },
            { exp.SN8(0x030fbc54), exp.SN8(0xf89be510), exp.SN8(0x07641af0), exp.SN8(0xfcf043ac), exp.SN8(0xfcf043ac), exp.SN8(0x07641af0), exp.SN8(0xf89be510), exp.SN8(0x030fbc54) },
            { exp.SN8(0x018f8b84), exp.SN8(0xfb8e3130), exp.SN8(0x06a6d988), exp.SN8(0xf8275a10), exp.SN8(0x07d8a5f0), exp.SN8(0xf9592678), exp.SN8(0x0471ced0), exp.SN8(0xfe70747c) },
            { exp.SN8(0x00000000), exp.SN8(0x00000000), exp.SN8(0x00000000), exp.SN8(0x00000000), exp.SN8(0x00000000), exp.SN8(0x00000000), exp.SN8(0x00000000), exp.SN8(0x00000000) },
            { exp.SN8(0xfe70747c), exp.SN8(0x0471ced0), exp.SN8(0xf9592678), exp.SN8(0x07d8a5f0), exp.SN8(0xf8275a10), exp.SN8(0x06a6d988), exp.SN8(0xfb8e3130), exp.SN8(0x018f8b84) },
            { exp.SN8(0xfcf043ac), exp.SN8(0x07641af0), exp.SN8(0xf89be510), exp.SN8(0x030fbc54), exp.SN8(0x030fbc54), exp.SN8(0xf89be510), exp.SN8(0x07641af0), exp.SN8(0xfcf043ac) },
            { exp.SN8(0xfb8e3130), exp.SN8(0x07d8a5f0), exp.SN8(0xfe70747c), exp.SN8(0xf9592678), exp.SN8(0x06a6d988), exp.SN8(0x018f8b84), exp.SN8(0xf8275a10), exp.SN8(0x0471ced0) },
            { exp.SN8(0xfa57d868), exp.SN8(0x05a82798), exp.SN8(0x05a82798), exp.SN8(0xfa57d868), exp.SN8(0xfa57d868), exp.SN8(0x05a82798), exp.SN8(0x05a82798), exp.SN8(0xfa57d868) },
            { exp.SN8(0xf9592678), exp.SN8(0x018f8b84), exp.SN8(0x07d8a5f0), exp.SN8(0x0471ced0), exp.SN8(0xfb8e3130), exp.SN8(0xf8275a10), exp.SN8(0xfe70747c), exp.SN8(0x06a6d988) },
            { exp.SN8(0xf89be510), exp.SN8(0xfcf043ac), exp.SN8(0x030fbc54), exp.SN8(0x07641af0), exp.SN8(0x07641af0), exp.SN8(0x030fbc54), exp.SN8(0xfcf043ac), exp.SN8(0xf89be510) },
            { exp.SN8(0xf8275a10), exp.SN8(0xf9592678), exp.SN8(0xfb8e3130), exp.SN8(0xfe70747c), exp.SN8(0x018f8b84), exp.SN8(0x0471ced0), exp.SN8(0x06a6d988), exp.SN8(0x07d8a5f0) },
            { exp.SN8(0xf8000000), exp.SN8(0xf8000000), exp.SN8(0xf8000000), exp.SN8(0xf8000000), exp.SN8(0xf8000000), exp.SN8(0xf8000000), exp.SN8(0xf8000000), exp.SN8(0xf8000000) },
            { exp.SN8(0xf8275a10), exp.SN8(0xf9592678), exp.SN8(0xfb8e3130), exp.SN8(0xfe70747c), exp.SN8(0x018f8b84), exp.SN8(0x0471ced0), exp.SN8(0x06a6d988), exp.SN8(0x07d8a5f0) },
            { exp.SN8(0xf89be510), exp.SN8(0xfcf043ac), exp.SN8(0x030fbc54), exp.SN8(0x07641af0), exp.SN8(0x07641af0), exp.SN8(0x030fbc54), exp.SN8(0xfcf043ac), exp.SN8(0xf89be510) },
            { exp.SN8(0xf9592678), exp.SN8(0x018f8b84), exp.SN8(0x07d8a5f0), exp.SN8(0x0471ced0), exp.SN8(0xfb8e3130), exp.SN8(0xf8275a10), exp.SN8(0xfe70747c), exp.SN8(0x06a6d988) }
        };
    }

    public class SbcCRC
    {
        public static byte[] crc_table = {
            0x00, 0x1D, 0x3A, 0x27, 0x74, 0x69, 0x4E, 0x53,
            0xE8, 0xF5, 0xD2, 0xCF, 0x9C, 0x81, 0xA6, 0xBB,
            0xCD, 0xD0, 0xF7, 0xEA, 0xB9, 0xA4, 0x83, 0x9E,
            0x25, 0x38, 0x1F, 0x02, 0x51, 0x4C, 0x6B, 0x76,
            0x87, 0x9A, 0xBD, 0xA0, 0xF3, 0xEE, 0xC9, 0xD4,
            0x6F, 0x72, 0x55, 0x48, 0x1B, 0x06, 0x21, 0x3C,
            0x4A, 0x57, 0x70, 0x6D, 0x3E, 0x23, 0x04, 0x19,
            0xA2, 0xBF, 0x98, 0x85, 0xD6, 0xCB, 0xEC, 0xF1,
            0x13, 0x0E, 0x29, 0x34, 0x67, 0x7A, 0x5D, 0x40,
            0xFB, 0xE6, 0xC1, 0xDC, 0x8F, 0x92, 0xB5, 0xA8,
            0xDE, 0xC3, 0xE4, 0xF9, 0xAA, 0xB7, 0x90, 0x8D,
            0x36, 0x2B, 0x0C, 0x11, 0x42, 0x5F, 0x78, 0x65,
            0x94, 0x89, 0xAE, 0xB3, 0xE0, 0xFD, 0xDA, 0xC7,
            0x7C, 0x61, 0x46, 0x5B, 0x08, 0x15, 0x32, 0x2F,
            0x59, 0x44, 0x63, 0x7E, 0x2D, 0x30, 0x17, 0x0A,
            0xB1, 0xAC, 0x8B, 0x96, 0xC5, 0xD8, 0xFF, 0xE2,
            0x26, 0x3B, 0x1C, 0x01, 0x52, 0x4F, 0x68, 0x75,
            0xCE, 0xD3, 0xF4, 0xE9, 0xBA, 0xA7, 0x80, 0x9D,
            0xEB, 0xF6, 0xD1, 0xCC, 0x9F, 0x82, 0xA5, 0xB8,
            0x03, 0x1E, 0x39, 0x24, 0x77, 0x6A, 0x4D, 0x50,
            0xA1, 0xBC, 0x9B, 0x86, 0xD5, 0xC8, 0xEF, 0xF2,
            0x49, 0x54, 0x73, 0x6E, 0x3D, 0x20, 0x07, 0x1A,
            0x6C, 0x71, 0x56, 0x4B, 0x18, 0x05, 0x22, 0x3F,
            0x84, 0x99, 0xBE, 0xA3, 0xF0, 0xED, 0xCA, 0xD7,
            0x35, 0x28, 0x0F, 0x12, 0x41, 0x5C, 0x7B, 0x66,
            0xDD, 0xC0, 0xE7, 0xFA, 0xA9, 0xB4, 0x93, 0x8E,
            0xF8, 0xE5, 0xC2, 0xDF, 0x8C, 0x91, 0xB6, 0xAB,
            0x10, 0x0D, 0x2A, 0x37, 0x64, 0x79, 0x5E, 0x43,
            0xB2, 0xAF, 0x88, 0x95, 0xC6, 0xDB, 0xFC, 0xE1,
            0x5A, 0x47, 0x60, 0x7D, 0x2E, 0x33, 0x14, 0x09,
            0x7F, 0x62, 0x45, 0x58, 0x0B, 0x16, 0x31, 0x2C,
            0x97, 0x8A, 0xAD, 0xB0, 0xE3, 0xFE, 0xD9, 0xC4
        };
    }


}