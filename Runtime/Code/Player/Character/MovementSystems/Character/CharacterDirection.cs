using System;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    public class CharacterDirection : MonoBehaviour {
        public CharacterMovement characterMovement;

        private void LateUpdate() {
            characterMovement.TryUpdateDirection();
        }
    }
}