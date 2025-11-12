using Code.Network.StateSystem.Structures;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    public class CharacterInputGroup : InputCommandGroup<CharacterInputData, CharacterInputDiff> {
        public CharacterInputGroup() {}
        public CharacterInputGroup(CharacterInputData[] inputs) : base(inputs) {}
    }

    public static class CharacterInputGroupSerializer {
        public static void WriteCharacterInputGroup(this NetworkWriter writer, CharacterInputGroup inputGroup) {
            // Diffs are encoded first because baseInput is read as if it is the last/only entry (it reads all remaining bytes as custom data)
            writer.Write(inputGroup.diffs);
            writer.Write(inputGroup.baseInput);
        }

        public static CharacterInputGroup ReadCharacterInputGroup(this NetworkReader reader) {
            var diffs = reader.Read<CharacterInputDiff[]>();
            var input = reader.Read<CharacterInputData>();
            return new CharacterInputGroup() {
                baseInput = input,
                diffs = diffs,
            };
        }
    }
}