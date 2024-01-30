namespace Code.Player.Character.API {

    /// <summary>
    /// Allows TS to hook into the movement system and change values.
    /// </summary>
    public class CharacterMoveModifier {
        public float speedMultiplier;
        public bool blockSprint;
        public bool blockJump;
    }
}