using Code.Network.StateSystem.Structures;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    public class CharacterInputDiff : InputCommandDiff {
        public byte[] data;
    }

    public static class CharacterInputDiffSerializer {
        public static void WriteCharacterInputDiff(this NetworkWriter writer, CharacterInputDiff diff) {
            writer.WriteBytes(diff.data, 0, diff.data.Length);
        }

        public static CharacterInputDiff ReadCharacterInputDiff(this NetworkReader reader) {
            var data = new byte[reader.Remaining];
            data = reader.ReadBytes(data, reader.Remaining);
            return new CharacterInputDiff() {
                data = data
            };
        }

        private static void WriteEntry(NetworkWriter writer, CharacterInputDiff diff) {
            writer.Write<byte>((byte) diff.data.Length); // TODO: Max diff size will be 255 bytes. This is huge, but possible if we have tons of command changes
            WriteCharacterInputDiff(writer, diff);
        }
        
        private static CharacterInputDiff ReadEntry(NetworkReader reader) {
            byte size = reader.Read<byte>();
            byte[] data = reader.ReadBytes(size);
            var diffReader = NetworkReaderPool.Get(data);
            var inputDiff = ReadCharacterInputDiff(diffReader);
            NetworkReaderPool.Return(diffReader);
            return inputDiff;
        }
        
        public static void WriteCharacterInputDiffArray(this NetworkWriter writer, CharacterInputDiff[] diffArray) {
            if (diffArray == null) return;
            writer.Write<byte>((byte) diffArray.Length);
            foreach (var diff in diffArray) {
                WriteEntry(writer, diff);
            }
        }
        
        public static CharacterInputDiff[] ReadCharacterInputDiffArray(this NetworkReader reader) {
            if (reader.Remaining == 0) return null;
            
            CharacterInputDiff[] diffs = new CharacterInputDiff[reader.Read<byte>()];
            for (var i = 0; i < diffs.Length; i++) {
                Debug.Log($"Reading {i}/{diffs.Length}");
                diffs[i] = ReadEntry(reader);
            }
            return diffs;
        }
    }
}