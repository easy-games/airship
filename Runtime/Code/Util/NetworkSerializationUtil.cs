using System;

namespace Code.Util {
    public static class NetworkSerializationUtil {
        public static ushort CompressToUshort(float value) {
            double scaled = (double)value * 1000.0;
            int quantised = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
            quantised = Math.Clamp(quantised, 0, ushort.MaxValue);
            return (ushort)quantised;
        }
        
        public static short CompressToShort(float value) {
            double scaled = (double)value * 1000.0;
            int quantised = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
            quantised = Math.Clamp(quantised, short.MinValue, short.MaxValue);
            return (short)quantised;
        }

        public static float DecompressUShort(ushort value) {
            return value / 1000f;
        }

        public static float DecompressShort(short value) {
            return value / 1000f;
        }

        public static int CompressToInt(float value) {
            double scaled = (double)value * 1000.0;
            return (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        }

        public static float DecompressInt(int value) {
            return value / 1000f;
        }
    }
}