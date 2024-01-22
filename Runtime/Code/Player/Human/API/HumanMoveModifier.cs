namespace Code.Player.Human.API {

    /// <summary>
    /// Allows TS to hook into the movement system and change values.
    /// </summary>
    public class HumanMoveModifier {
        public float speedMultiplier;
        public bool blockSprint;
        public bool blockJump;
    }
}