using System.Collections.Generic;
using System.Linq;

namespace CsToTs.TypeScript {

    public class MemberDefinition {

        public MemberDefinition(string name, string type, bool isNullable = false, IEnumerable<string> decorators = null, bool isStatic = false) {
            Name = name;
            Type = type;
            IsNullable = isNullable;
            Decorators = (decorators?.ToList() ?? new List<string>()).AsReadOnly();
            IsStatic = isStatic;
        }

        public string Name { get; }
        public string Type { get; }
        public bool IsNullable { get; }
        public IReadOnlyCollection<string> Decorators { get; }
        
        public bool IsStatic { get; }
    }
}