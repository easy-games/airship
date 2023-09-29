using System.Collections.Generic;
using Newtonsoft.Json;

namespace Luau
{
    [System.Serializable]
    public class LuauMetadataProperty
    {
        public string name;
        public string type;
        public string modifiers;
    }

    [System.Serializable]
    public class LuauMetadata
    {
        public List<LuauMetadataProperty> properties;

        public static LuauMetadata FromJson(string json)
        {
            return JsonConvert.DeserializeObject<LuauMetadata>(json);
        }
    }
}
