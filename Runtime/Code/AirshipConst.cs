// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Code {
    [LuauAPI][Preserve]
    public static class AirshipConst {
        public const int playerVersion = 20;
        public static readonly IReadOnlyList<string> playerFlags = new string[] {
            "SkipLoading",
        };


        /// <summary>
        /// The server will kick clients that have a playerVersion lower than this value.
        /// </summary>
        public const int minAcceptedPlayerVersionOnServer = 20;
    }
}
