using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Mirror;
using UnityEngine;
using UnityEngine.Profiling;

namespace Code.Bootstrap {

    class CachedCompressedFile {
        public byte[] compressedBytes;
        public int compressedLength;

        public CachedCompressedFile(byte[] compressedBytes, int compressedLength) {
            this.compressedBytes = compressedBytes;
            this.compressedLength = compressedLength;
        }
    }
    
    public static class LuauScriptsDtoSerializer {
        private static Zstd.Zstd zstd = new(1024 * 4);
        private static readonly Dictionary<string, CachedCompressedFile> compressedFileCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnReload() {
            compressedFileCache.Clear();
        }
        
        public static void WriteLuauScriptsDto(this NetworkWriter writer, LuauScriptsDto scripts) {
            Profiler.BeginSample("WriteLuauScriptsDto");
            writer.WriteInt(scripts.files.Count);
            foreach (var pair in scripts.files) {
                string packageId = pair.Key;
                writer.WriteString(packageId);
                writer.WriteInt(pair.Value.Count);
                foreach (var file in pair.Value) {
                    writer.WriteString(file.path);

                    string cacheId = $"{packageId}:{file.path}";
                    if (compressedFileCache.TryGetValue(cacheId, out var cache)) {
                        writer.WriteInt(cache.compressedLength);
                        writer.WriteBytes(cache.compressedBytes, 0, cache.compressedLength);
                    } else {
                        // Compress the byte array
                        Profiler.BeginSample("Luau Compress");
                        var maxCompressionSize = Zstd.Zstd.GetCompressionBound(file.bytes);
                        byte[] compressedBytes = new byte[maxCompressionSize];
                        var compressedSize = zstd.Compress(file.bytes, compressedBytes);
                        writer.WriteInt(compressedSize);
                        writer.WriteBytes(compressedBytes, 0, compressedSize);
                        
                        compressedFileCache.Add(cacheId, new CachedCompressedFile(compressedBytes, compressedSize));
                    }
                    
                    writer.WriteBool(file.airshipBehaviour);
                    Profiler.EndSample();
                }
            }
            Profiler.EndSample();
        }

        public static LuauScriptsDto ReadLuauScriptsDto(this NetworkReader reader) {
            LuauScriptsDto dto = new LuauScriptsDto();
            int packagesLength = reader.ReadInt();
            for (int pkgI = 0; pkgI < packagesLength; pkgI++) {
                string packageId = reader.ReadString();
                int length = reader.ReadInt();
                List<LuauFileDto> files = new();
                dto.files.Add(packageId, files);

                for (int i = 0; i < length; i++) {
                    LuauFileDto script = new LuauFileDto();
                    script.path = reader.ReadString();

                    // Read the compressed bytes
                    var compressedBytesLen = reader.ReadInt();
                    byte[] compressedBytes = ArrayPool<byte>.Shared.Rent(compressedBytesLen);
                    reader.ReadBytes(compressedBytes, compressedBytesLen);

                    // Decompress the bytes
                    var bytes = ArrayPool<byte>.Shared.Rent(Zstd.Zstd.GetDecompressionBound(compressedBytes));
                    zstd.Decompress(new ReadOnlySpan<byte>(compressedBytes, 0, compressedBytesLen), bytes);
                    Buffer.BlockCopy(bytes, 0, script.bytes, 0, bytes.Length);

                    script.airshipBehaviour = reader.ReadBool();

                    files.Add(script);
                }
            }

            // Debug.Log("scripts dto size: " + (totalBytes / 1000) + " KB.");
            return dto;
        }
    }
}