// ReSharper disable InconsistentNaming
namespace Code {
    public static class AirshipConst {
        /// <summary>
        /// <para>Version number is incremented when you need to lock out clients on older versions.</para>
        ///
        /// <para>Example: adding a method that TypeScript needs to work properly.</para>
        ///
        /// <para>You should avoid having to increment player version whenever possible. Instead, make
        /// TypeScript work in cases where the method doesn't exist.</para>
        /// </summary>
        public const int playerVersion = 1;

        /// <summary>
        /// The server will kick clients that have a playerVersion lower than this value.
        /// </summary>
        public const int minAcceptedPlayerVersionOnServer = 1;
    }
}