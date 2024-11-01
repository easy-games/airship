using Code.Player.Character.API;

public class CharacterStateSyncData {
    public CharacterState state = CharacterState.Idle;
    public bool grounded = true;
    public bool sprinting = false;
    public bool crouching = false;

    // override object.Equals
    public override bool Equals(object obj) {
        CharacterStateSyncData data = (CharacterStateSyncData)obj;
        return this.state == data.state &&
               this.grounded == data.grounded &&
               this.sprinting == data.sprinting &&
               this.crouching == data.crouching;
    }
    public override int GetHashCode() {
        unchecked {
            int hashCode = state.GetHashCode();
            hashCode = (hashCode * 397) ^ grounded.GetHashCode();
            hashCode = (hashCode * 397) ^ sprinting.GetHashCode();
            hashCode = (hashCode * 397) ^ crouching.GetHashCode();
            return hashCode;
        }
    }
}