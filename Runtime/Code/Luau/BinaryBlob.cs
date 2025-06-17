using System;
using Mirror;
using Code.Util;
using UnityEngine;

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
            int length = Math.Max(other.data.Length, data.Length);
            var neededBytes = (int) Math.Ceiling(length / 8f);
            // Debug.Log($"Longest custom data is {length} bytes. Change array needs length {neededBytes} bytes");
            byte[] changeBytes = new byte[neededBytes];
            var writer = NetworkWriterPool.Get();
            writer.WriteByte((byte) changeBytes.Length);
            // Debug.Log($"Prepending with {changeBytes.Length} change flag bytes.");

            var changedByteWriter = NetworkWriterPool.Get();
            for (int i = 0; i < length; i++) {
                int bitIndex = i % 8;
                int byteIndex = i / 8;
                // Debug.Log($"Reading bit {bitIndex} of byte {byteIndex}. Bit number: {i}");
                
                // We ran out of new data. we will continue to write changed bits, but we will not write byte data
                if (i > other.data.Length - 1) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    // Debug.Log($"Byte {byteIndex} does not exist on new data. Setting flag only");
                    continue;
                }

                // We ran out of base data. We will continue to write change bits and the new data
                if (i > data.Length - 1) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    changedByteWriter.Write(other.data[i]);
                    // Debug.Log($"Byte {byteIndex} does not exist on old data. Setting flag and writing new byte {other.data[i]}");
                    continue;
                }

                // We have values to compare
                if (other.data[i] == data[i]) {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, false);
                    // Debug.Log($"Byte {byteIndex} is equal. {other.data[i]} == {data[i]}");
                    // Do not write unchanged data
                } else {
                    BitUtil.SetBit(ref changeBytes[byteIndex], bitIndex, true);
                    changedByteWriter.Write(other.data[i]);
                    // Debug.Log($"Byte {byteIndex} does not match. {other.data[i]} != {data[i]}. Writing diff.");
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
            // Debug.Log($"Got diff of {bytes.Length} bytes.");
            // first byte in bytes will be the number of bytes to read as the change bit flags
            // each bit of the change flag bytes refers to the index of the base byte array
            // ie. bit 0 is byte zero of the base data. If the flag is true, we should read
            // a byte from the diff data and replace index 0 with the new byte data.
            byte changedLength = bytes[0];
            if (changedLength == 0) return new BinaryBlob() {
                dataSize = dataSize,
                data = (byte[]) data.Clone(),
            };
            // Debug.Log($"Change length is {changedLength}");
            
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
                    // Debug.Log($"Byte at {existingByteIndex} update is {update}. Read index is {diffReadIndex} of {bytes.Length} total bytes");
                    
                    // If we have run out of diff data, but the bit flag says we should update the position,
                    // we are going to delete the remaining bytes by not writing them. This happens when
                    // the diff is supposed to shorten the base data.
                    if (diffReadIndex > (bytes.Length - 1) && update) {
                        // Debug.Log($"{diffReadIndex} is greater than diff length, but update is true. Ignoring write to remove end bytes.");
                        continue;
                    }

                    if (existingByteIndex > (data.Length - 1) && !update) {
                        //Debug.Log($"{existingByteIndex} is greater than data length, and update is false. Ignoring write since these are unneeded flag bits.");
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
