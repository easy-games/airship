using System.Collections.Generic;
using Newtonsoft.Json;

namespace Luau
{
    [System.Serializable]
    public class LuauMetadataProperty
    {
        public string name;
        public string type;
        public List<string> modifiers;
        public string serializedValue;

        public bool HasModifier(string modifier)
        {
            return modifier.Contains(modifier);
        }

        public LuauMetadataProperty Clone()
        {
            var clone = new LuauMetadataProperty();
            clone.name = name;
            clone.type = type;
            clone.modifiers = new List<string>(modifiers);
            clone.serializedValue = serializedValue;
            return clone;
        }
    }

    [System.Serializable]
    public class LuauMetadata
    {
        public List<LuauMetadataProperty> properties;

        public static LuauMetadata FromJson(string json)
        {
            return JsonConvert.DeserializeObject<LuauMetadata>(json);
        }

        public LuauMetadataProperty FindProperty<T>(string name)
        {
            foreach (var property in properties)
            {
                if (property.name == name)
                {
                    return property;
                }
            }

            return null;
        }
    }
}
