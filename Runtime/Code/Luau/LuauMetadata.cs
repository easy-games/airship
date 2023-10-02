using System.Collections.Generic;
using Newtonsoft.Json;

namespace Luau
{
    [System.Serializable]
    public class LuauMetadataProperty<T>
    {
        public string name;
        public string type;
        public List<string> modifiers;
        public T value;

        public bool HasModifier(string modifier)
        {
            return modifier.Contains(modifier);
        }

        public LuauMetadataProperty<T> Clone()
        {
            var clone = new LuauMetadataProperty<T>();
            clone.name = name;
            clone.type = type;
            clone.modifiers = new List<string>(modifiers);
            clone.value = value;
            return clone;
        }
    }

    [System.Serializable]
    public class LuauMetadata
    {
        public List<LuauMetadataProperty<object>> properties;

        public static LuauMetadata FromJson(string json)
        {
            return JsonConvert.DeserializeObject<LuauMetadata>(json);
        }

        public LuauMetadataProperty<T> FindProperty<T>(string name)
        {
            foreach (var property in properties)
            {
                if (property.name == name)
                {
                    return property as LuauMetadataProperty<T>;
                }
            }

            return null;
        }
    }
}
