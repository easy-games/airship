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
                    writer.WriteInt32(file.bytes.Length);
                    writer.WriteBytes(file.bytes, 0, file.bytes.Length);
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
                    int bytesLength = reader.ReadInt32();
                    byte[] bytes = new byte[bytesLength];
                    reader.ReadBytes(ref bytes, bytesLength);
                    script.bytes = bytes;
                    script.airshipBehaviour = reader.ReadBoolean();

                    files[i] = script;
                }
            }

            Debug.Log("scripts dto size: " + (reader.Length / 1000) + " KB.");
            return dto;
        }
    }
}