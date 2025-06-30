using Assets.Luau;
using Code.Network.StateSystem.Structures;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    public class CharacterStateDiff : StateDiff {
        // Diff data encoded as a byte array. Will be decoded by ApplyDiff.
        public byte[] data;
    }

    public static class CharacterDiffDataSerializer {
        public static void WriteCharacterStateDiff(this NetworkWriter writer, CharacterStateDiff diff) {
            writer.Write(diff.baseTick);
            writer.Write(diff.crc32);
            writer.WriteBytes(diff.data, 0, diff.data.Length);
        }
        
        public static CharacterStateDiff ReadCharacterStateDiff(this NetworkReader reader) {
            var baseTick = reader.Read<uint>();
            var crc32 = reader.Read<uint>();
            var data = new byte[reader.Remaining];
            data = reader.ReadBytes(data, reader.Remaining);
            return new CharacterStateDiff() {
                baseTick = baseTick,
                crc32 = crc32,
                data = data,
            };
        }
    }
}