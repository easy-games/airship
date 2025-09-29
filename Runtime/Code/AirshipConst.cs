// ReSharper disable InconsistentNaming

namespace Code {
    [LuauAPI]
    public static class AirshipConst {
        public const int playerVersion = 16;

        /// <summary>
        /// The server will kick clients that have a playerVersion lower than this value.
        /// </summary>
        public const int minAcceptedPlayerVersionOnServer = 15;
    }
}
