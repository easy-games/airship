using System;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    [LuauAPI]
    public class BasicCharacterAnimationSyncData
    {
        public BasicCharacterState state = BasicCharacterState.Idle;
        public bool grounded = true;
        public bool sprinting = false;
        public bool crouching = false;
        public bool jumping = false;
        public Vector3 localVelocity = Vector3.zero;
        public Vector3 lookVector = Vector3.zero;


        // override object.Equals
        public override bool Equals(object obj) {
            BasicCharacterAnimationSyncData data = (BasicCharacterAnimationSyncData)obj;
            return this.state == data.state &&
                   this.grounded == data.grounded &&
                   this.sprinting == data.sprinting &&
                   this.crouching == data.crouching && 
                   this.lookVector == data.lookVector && 
                   this.localVelocity == data.localVelocity;
        }

        public override int GetHashCode() {
            unchecked {
                int hashCode = state.GetHashCode();
                hashCode = (hashCode * 397) ^ grounded.GetHashCode();
                hashCode = (hashCode * 397) ^ sprinting.GetHashCode();
                hashCode = (hashCode * 397) ^ crouching.GetHashCode();
                hashCode = (hashCode * 397) ^ lookVector.GetHashCode();
                hashCode = (hashCode * 397) ^ localVelocity.GetHashCode();
                return hashCode;
            }
        }
    }
}