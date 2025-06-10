using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Mirror;
using UnityEngine;
using UnityEngine.Profiling;

namespace Code.Bootstrap {
    public static class LuauScriptsDtoSerializer {
        public static void WriteLuauScriptsDto(this NetworkWriter writer, LuauScriptsDto scripts) {
            Profiler.BeginSample("WriteLuauScriptsDto");
            writer.WriteInt(scripts.files.Count);
            foreach (var pair in scripts.files) {
                string packageId = pair.Key;
                writer.WriteString(packageId);
                writer.WriteInt(pair.Value.Count);
                foreach (var file in pair.Value) {
                    writer.WriteString(file.path);

                    // Compress the byte array
                    Profiler.BeginSample("Array Compress");
                    byte[] compressedBytes;
                    using (MemoryStream ms = new MemoryStream()) {
                        using (DeflateStream deflateStream = new DeflateStream(ms, CompressionMode.Compress)) {
                            deflateStream.Write(file.bytes, 0, file.bytes.Length);
                        }
                        compressedBytes = ms.ToArray();
                    }
                    writer.WriteArray(compressedBytes);
                    writer.WriteBool(file.airshipBehaviour);
                    Profiler.EndSample();
                }
            }
            Profiler.EndSample();
        }

        public static LuauScriptsDto ReadLuauScriptsDto(this NetworkReader reader) {
            var totalBytes = reader.Remaining;
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

                    var byteArray = reader.ReadArray<byte>();
                    using (MemoryStream compressedStream = new MemoryStream(byteArray)) {
                        using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress)) {
                            using (MemoryStream outputStream = new MemoryStream()) {
                                deflateStream.CopyTo(outputStream);
                                script.bytes = outputStream.ToArray();
                            }
                        }
                    }

                    script.airshipBehaviour = reader.ReadBool();

                    files.Add(script);
                }
            }

            // Debug.Log("scripts dto size: " + (totalBytes / 1000) + " KB.");
            return dto;
        }
    }
}