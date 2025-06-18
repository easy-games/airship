using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Code.Zstd;
using UnityEngine;
using UnityEngine.Serialization;
using static LuauCore;

namespace Assets.Luau {
    [Serializable]
    public class BinaryBlob : IEquatable<BinaryBlob> {
        public BinaryBlob() {
            data = new byte[] { };
            dataSize = 0;
        }
        
        public BinaryBlob(byte[] bytes) {
            if (bytes.Length > uint.MaxValue) {
                throw new Exception("Length of binary blob data exceeds " + int.MaxValue + " bytes.");
            }
            dataSize = bytes.Length;
            data = bytes;
        }
        
        public int dataSize;
        public byte[] data;

        public bool Equals(BinaryBlob other) {
            return this.dataSize == other?.dataSize;
        }
    }
}
