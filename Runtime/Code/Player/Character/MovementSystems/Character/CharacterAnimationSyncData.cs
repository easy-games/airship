using System;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    [LuauAPI]
    public struct CharacterAnimationSyncData {
        public CharacterState state;
        public bool grounded;
        public bool sprinting;
        public bool crouching;
        public bool jumping;
        public Vector3 localVelocity;
        public Vector3 lookVector;

        public static CharacterAnimationSyncData Default => new CharacterAnimationSyncData {
            grounded = true,
            state = CharacterState.Idle,
        };

        // override object.Equals
        public override bool Equals(object obj) {
            var data = (CharacterAnimationSyncData)obj;
            return state == data.state &&
                   grounded == data.grounded &&
                   sprinting == data.sprinting &&
                   crouching == data.crouching &&
                   lookVector == data.lookVector &&
                   jumping == data.jumping &&
                   localVelocity == data.localVelocity;
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = state.GetHashCode();
                hashCode = (hashCode * 397) ^ grounded.GetHashCode();
                hashCode = (hashCode * 397) ^ sprinting.GetHashCode();
                hashCode = (hashCode * 397) ^ crouching.GetHashCode();
                hashCode = (hashCode * 397) ^ lookVector.GetHashCode();
                hashCode = (hashCode * 397) ^ localVelocity.GetHashCode();
                hashCode = (hashCode * 397) ^ jumping.GetHashCode();
                return hashCode;
            }
        }
    }
}