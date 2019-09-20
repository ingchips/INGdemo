using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using System.Linq;

namespace INGdemo.Lib
{
    abstract public class AngleEstimator
    {
        internal string FDescription;

        public string Description { get => FDescription;  }

        static public double FreqOfChannelIndexMHz(int ChannelIndex)
        {
            if (0 > ChannelIndex)
                return 0.0;

            if (ChannelIndex <= 10)
                return 2404.0 + 2 * ChannelIndex;
            else if (ChannelIndex <= 36)
                return 2428.0 + 2 * (ChannelIndex - 11);
            else
                return 0.0;
        }

        public double Estimate(int ChannelIndex, sbyte[] iqs)
        {
            var r = iqs.Select((x, i) => new { Index = i, Value = x })
                        .GroupBy(x => x.Index / 2)
                        .Select(x => x.Select(v => v.Value).ToList())
                        .Select(x => new Complex(x[0] / 128.0, x[1] / 128.0))
                        .ToArray();
            return Estimate(FreqOfChannelIndexMHz(ChannelIndex) * 1000000, r);
        }

        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
                return min;
            if (value.CompareTo(max) > 0)
                return max;

            return value;
        }

        abstract internal double Estimate(double fc, Complex[] iqs); 
    }

    public class AngleEstimatorFactory
    {
        public static AngleEstimator CreateEstimator(byte cfg)
        {
            switch (cfg)
            {
                case 1:
                    return new AngleEstimator1();
                default:
                    return null;
            }            
        }
    }

    class AngleEstimator1 : AngleEstimator
    {
        const double Radius = 0.03;

        internal AngleEstimator1()
        {
            FDescription = "Switching Pattern: [0, 1]";
        }

        override internal double Estimate(double fc, Complex[] iqs)
        {
            if (iqs.Length < 9) return 0.0;

            double a = 300000000.0 / (4 * Math.PI * fc * Radius);

            var r = iqs.Skip(8)
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / 2)
                .Select(x => x.Select(v => v.Value).ToList())
                .Select(x => (x[0] / x[1]).Phase)
                .Average() * a;

            return Math.Acos(Clamp(r, -1, 1));
        }
    }
}
