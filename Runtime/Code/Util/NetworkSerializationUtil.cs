using System;
using UnityEngine;

namespace Code.Util {
    public static class NetworkSerializationUtil {
        public static ushort CompressToUshort(float value) {
            return (ushort) CompressToUshort((double) value);
        }
        
        public static ushort CompressToUshort(double value) {
            double scaled = value * 1000.0;
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

        /// <summary>
        /// Quantizes a float to the network precision (0.001) to ensure deterministic behavior across platforms.
        /// Use this after physics calculations to keep client and server in sync.
        /// </summary>
        public static float QuantizeFloat(float value) {
            return DecompressInt(CompressToInt(value));
        }

        /// <summary>
        /// Quantizes a Vector3 to the network precision (0.001) to ensure deterministic behavior across platforms.
        /// Use this after physics calculations to keep client and server in sync.
        /// </summary>
        public static Vector3 QuantizeVector3(Vector3 value) {
            return new Vector3(
                QuantizeFloat(value.x),
                QuantizeFloat(value.y),
                QuantizeFloat(value.z)
            );
        }
    }
}