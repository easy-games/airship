using System;
using System.Runtime.InteropServices;
using Mirror;
using Code.Util;
using Code.Zstd;

namespace Assets.Luau {
    [Serializable]
    public class BinaryBlob : IEquatable<BinaryBlob> {
        public readonly byte[] Data;
        
        /// <summary>
        /// Gets the size of the data. This is the same as <c>data.Length</c>.
        /// </summary>
        public int DataSize => Data.Length;

        /// <summary>
        /// Gets the decompressed size of the underlying data, if the data is currently compressed. If not compressed,
        /// then this returns <c>dataSize</c>.
        /// </summary>
        public int DecompressedDataSize => IsCompressed ? Zstd.GetDecompressionBound(Data) : Data.Length;

        /// <summary>
        /// Returns <c>true</c> if the BinaryBlob data appears to be compressed.
        /// </summary>
        public bool IsCompressed => Data.Length > 0 && Data[0] == 1;
        
        public BinaryBlob() {
            Data = new byte[] { };
        }
        
        public BinaryBlob(byte[] bytes) {
            Data = bytes;
        }

        public BinaryBlob(IntPtr nativeBinaryBlobPtr) {
            var marshal = LuauBinaryBlobMarshal.FromIntPtr(nativeBinaryBlobPtr);
            Data = marshal.ReadData();
        }

        public bool Equals(BinaryBlob other) {
            return this.DataSize == other?.DataSize;
        }

        public BinaryBlob Clone() {
            var clonedData = new byte[Data.Length];
            Array.Copy(Data, clonedData, Data.Length);
            return new BinaryBlob(clonedData);
        }

        public byte[] CreateDiff(BinaryBlob other) {
            int length = Math.Max(other.Data.Length, Data.Length);
            var neededBytes = (int) Math.Ceiling(length / 8f);
            byte[] changeBytes = new byte[neededBytes];
            var writer = NetworkWriterPool.Get();
            writer.WriteByte((byte) changeBytes.Length);

            var changedByteWriter = NetworkWriterPool.Get();
            for (int i = 0; i < length; i++) {
                int bitIndex = i % 8;
                int byteIndex = i / 8;
                
                // We ran out of new data. we will continue to write changed bits, but we will not write byte data
                if (i > other.Data.Length - 1) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    continue;
                }

                // We ran out of base data. We will continue to write change bits and the new data
                if (i > Data.Length - 1) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    changedByteWriter.Write(other.Data[i]);
                    continue;
                }

                // We have values to compare
                if (other.Data[i] == Data[i]) {
                    // Byte values are equal. No need to write a new value
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, false);
                } else {
                    // Byte values don't match. Write new value 
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    changedByteWriter.Write(other.Data[i]);
                }
            }

            writer.WriteBytes(changeBytes, 0, changeBytes.Length);
            var newByteValues = changedByteWriter.ToArray();
            writer.WriteBytes(newByteValues, 0 ,newByteValues.Length);
            var bytes = writer.ToArray();
            
            NetworkWriterPool.Return(writer);
            NetworkWriterPool.Return(changedByteWriter);
            
            // Debug.Log($"Final diff is {bytes.Length} bytes");
            return bytes;
        }

        public BinaryBlob ApplyDiff(byte[] bytes) {
            // first byte in bytes will be the number of bytes to read as the change bit flags.
            // Each bit of the change flag bytes refers to the index of the base byte array
            // ie. bit 0 is byte zero of the base data. If the flag is true, we should read
            // a byte from the diff data and replace index 0 with the new byte data.
            byte changedLength = bytes[0];
            if (changedLength == 0) return Clone();
            
            var byteWriter = NetworkWriterPool.Get();
            var diffReadIndex = changedLength + 1; // + 1 for the byte used to encode the data size
            for (byte curByte = 0; curByte < changedLength; curByte++) {
                byte flags = bytes[curByte + 1];
                for (byte curBitOfByte = 0; curBitOfByte < 8; curBitOfByte++) {
                    // The index into the existing byte array that the bit flag refers to.
                    var existingByteIndex = curBitOfByte + (curByte * 8);
                    
                    // Should we update the byte at the current position?
                    bool update = BitUtil.GetBit(flags, curBitOfByte);
                    
                    // If we have run out of diff data, but the bit flag says we should update the position,
                    // we are going to delete the remaining bytes by not writing them. This happens when
                    // the diff is supposed to shorten the base data.
                    if (diffReadIndex > (bytes.Length - 1) && update) {
                        // The absence of a write here "removes" data from the end of the base data.
                        continue;
                    }

                    // If we run out of base data, but update wasn't set, it means we've reached the end of the changed flags. This happens when you have
                    // 7 bytes of data, but you have to include 1 full byte of change flags (8 bits). The last bit will be zero since there's no associated byte for that flag.
                    if (existingByteIndex > (Data.Length - 1) && !update) {
                        // We do nothing since this bit flag is meaningless.
                        continue;
                    }
                    
                    byteWriter.Write(update ? bytes[diffReadIndex] : Data[existingByteIndex]);
                    if (update) diffReadIndex++; // Move our read pointer forward since we read a byte from our diff data.
                }
            }

            var newBlob = new BinaryBlob(byteWriter.ToArray()); // new byte array with the diff applied
            NetworkWriterPool.Return(byteWriter);
            return newBlob;
        }
    }

    /// <summary>
    /// Strictly used for marshalling BinaryBlob data only.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LuauBinaryBlobMarshal {
        // Ordering of these fields must match BinaryBlob struct in native LuauPlugin:
        public readonly IntPtr Data;
        public readonly ulong DataSize;

        public static LuauBinaryBlobMarshal FromIntPtr(IntPtr ptr) {
            return Marshal.PtrToStructure<LuauBinaryBlobMarshal>(ptr);
        }

        public byte[] ReadData() {
            var bytes = new byte[DataSize];
            Marshal.Copy(Data, bytes, 0, (int)DataSize);
            return bytes;
        }
    }
}
