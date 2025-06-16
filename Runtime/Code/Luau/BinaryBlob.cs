using System;
using Mirror;
using Code.Util;

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

        public byte[] CreateDiff(BinaryBlob other) {
            var writer = NetworkWriterPool.Get();
            writer.WriteByte(0); // Write one byte to be set later. It will represent the number of changed flag bytes.
            byte changedFlagByteCount = 0;
            int length = Math.Max(other.data.Length, data.Length);
            byte[] changeBytes = new byte[length / 8];

            var changedByteWriter = NetworkWriterPool.Get();
            for (int i = 0; i < length; i++) {
                int bitIndex = i % 8;
                int byteIndex = i / 8;
                
                // We ran out of new data. we will continue to write changed bits, but we will not write byte data
                if (i > other.data.Length - 1) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    continue;
                }

                // We ran out of base data. We will continue to write change bits and the new data
                if (i > data.Length) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    changedByteWriter.Write(other.data[i]);
                    continue;
                }

                // We have values to compare
                if (other.data[i] == data[i]) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, false);
                    // Do not write unchanged data
                } else {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    changedByteWriter.Write(other.data[i]);
                }
            }
            writer.Write(changeBytes);
            writer.Write(changedByteWriter.ToArray());
            
            var bytes = writer.ToArray();
            bytes[0] = changedFlagByteCount;
            NetworkWriterPool.Return(writer);
            NetworkWriterPool.Return(changedByteWriter);
            return bytes;
        }

        public BinaryBlob ApplyDiff(byte[] bytes) {
            // first byte in bytes will be the number of bytes to read as the change bit flags
            // each bit of the change flag bytes refers to the index of the base byte array
            // ie. bit 0 is byte zero of the base data. If the flag is true, we should read
            // a byte from the diff data and replace index 0 with the new byte data.
            byte changedLength = bytes[0];
            if (changedLength == 0) return new BinaryBlob() {
                dataSize = dataSize,
                data = (byte[]) data.Clone(),
            };
            
            var byteWriter = NetworkWriterPool.Get();
            var diffReadIndex = changedLength + 1; // + 1 for the byte used to encode the data size
            for (byte curByte = 0; curByte < changedLength; curByte++) {
                byte flags = bytes[curByte + 1];
                for (byte curBitOfByte = 0; curBitOfByte < 8; curBitOfByte++) {
                    // The index into the existing byte array that the bit flag refers to.
                    var existingByteIndex = curBitOfByte + (curByte * 8);
                    
                    // // If the index referred to by the change bit is larger than the old data,
                    // // we have a diff that is adding data at the end of the base data. This
                    // // means we will always write the changes from the diff.
                    // if (existingByteIndex >= data.Length) {
                    //     byteWriter.Write(bytes[diffReadIndex]);
                    //     diffReadIndex++;
                    //     continue;
                    // }
                    
                    // Should we update the byte at the current position?
                    bool update = BitUtil.GetBit(flags, curBitOfByte);
                    
                    // If we have run out of diff data, but the bit flag says we should update the position,
                    // we are going to delete the remaining bytes by not writing them. This happens when
                    // the diff is supposed to shorten the base data.
                    if (diffReadIndex > bytes.Length && update) {
                        continue;
                    }
                    
                    byteWriter.Write(update ? bytes[diffReadIndex] : data[existingByteIndex]);
                    if (update) diffReadIndex++;
                }
            }
            
            var newBlob = new BinaryBlob() {
                data = byteWriter.ToArray(), // new byte array with the diff applied
                dataSize = byteWriter.Position // size of new byte array
            };
            NetworkWriterPool.Return(byteWriter);
            return newBlob;
        }
    }
}
