using System;
using System.IO;
using System.IO.Compression;
using FishNet.Serializing;
using UnityEngine;

namespace Code.Bootstrap {
    public static class LuauScriptsDtoSerializer {

        public static void WriteLuauScriptsDto(this Writer writer, LuauScriptsDto scripts) {
            writer.WriteInt32(scripts.files.Count);;
            foreach (var pair in scripts.files) {
                string packageId = pair.Key;
                writer.WriteString(packageId);
                writer.WriteInt32(pair.Value.Length);
                foreach (var file in pair.Value) {
                    writer.WriteString(file.path);

                    // Compress the byte array
                    byte[] compressedBytes;
                    using (MemoryStream ms = new MemoryStream()) {
                        using (DeflateStream deflateStream = new DeflateStream(ms, CompressionMode.Compress)) {
                            deflateStream.Write(file.bytes, 0, file.bytes.Length);
                        }
                        compressedBytes = ms.ToArray();
                    }
                    writer.WriteArray(compressedBytes);
                    writer.WriteBoolean(file.airshipBehaviour);
                }
            }
        }

        public static LuauScriptsDto ReadLuauScriptsDto(this Reader reader) {
            LuauScriptsDto dto = new LuauScriptsDto();
            int packagesLength = reader.ReadInt32();
            for (int pkgI = 0; pkgI < packagesLength; pkgI++) {
                string packageId = reader.ReadString();
                int length = reader.ReadInt32();
                LuauFileDto[] files = new LuauFileDto[length];
                dto.files.Add(packageId, files);

                for (int i = 0; i < length; i++) {
                    LuauFileDto script = new LuauFileDto();
                    script.path = reader.ReadString();

                    byte[] byteArray = null;
                    reader.ReadArray(ref byteArray);
                    using (MemoryStream compressedStream = new MemoryStream(byteArray)) {
                        using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress)) {
                            using (MemoryStream outputStream = new MemoryStream()) {
                                deflateStream.CopyTo(outputStream);
                                script.bytes = outputStream.ToArray();
                            }
                        }
                    }

                    script.airshipBehaviour = reader.ReadBoolean();

                    files[i] = script;
                }
            }

            Debug.Log("scripts dto size: " + (reader.Length / 1000) + " KB.");
            return dto;
        }
    }
}