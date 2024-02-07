using System;
using System.Collections.Generic;
using System.Linq;

namespace CsToTs.TypeScript {
    
    public class EnumDefinition {
        public EnumDefinition(Type clrType, string name, IEnumerable<EnumField> fields, bool skipDeclaration) {
            ClrType = clrType;
            Name = name;
            Fields = (fields?.ToList() ?? new List<EnumField>()).AsReadOnly();
            SkipDeclaration = skipDeclaration;
        }
        
        public Type ClrType { get; }
        public string Name { get; }
        public IReadOnlyCollection<EnumField> Fields { get; }
        
        public bool SkipDeclaration { get; }
    }
}