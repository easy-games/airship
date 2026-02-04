using System;

namespace Code.LuauAPI {
    [LuauAPI]
    public class GuidAPI : BaseLuaAPIClass {
        public override Type GetAPIType() {
            return typeof(Guid);
        }
    }
}