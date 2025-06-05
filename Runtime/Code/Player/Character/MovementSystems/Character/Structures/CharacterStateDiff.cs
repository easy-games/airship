using Assets.Luau;
using Code.Network.StateSystem.Structures;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    public class CharacterStateDiff : StateDiff {

        // Diff data encoded as a byte array. Will be decoded by ApplyDiff.
        public byte[] data;
    }
}