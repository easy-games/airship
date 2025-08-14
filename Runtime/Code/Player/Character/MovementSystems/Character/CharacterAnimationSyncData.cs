using System;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character {
    [LuauAPI]
    public class CharacterAnimationSyncData {
        public CharacterState state = CharacterState.Idle;
        public bool grounded = true;
        public bool sprinting = false;
        public bool crouching = false;
        public bool jumping = false;
        public Vector3 localVelocity = Vector3.zero;
        public Vector3 lookVector = Vector3.zero;


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