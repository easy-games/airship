using UnityEngine;

[LuauAPI]
public class CharacterAnimationSyncData {
    public CharacterState state = CharacterState.Idle;
    public bool grounded = true;
    public bool sprinting = false;
    public bool crouching = false;
    public Vector3 localVelocity = Vector3.zero;
    public Vector3 lookVector = Vector3.zero;


    // override object.Equals
    public override bool Equals(object obj) {
        CharacterAnimationSyncData data = (CharacterAnimationSyncData)obj;
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
