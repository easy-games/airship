using System;
using System.Collections.Generic;

namespace CsToTs.TypeScript {
    
    public class TypeDefinition {

        public TypeDefinition(Type clrType, string name, string declaration, bool skipDeclaration, string instanceDeclaration) {
            ClrType = clrType;
            Name = name;
            Declaration = declaration;
            Ctors = new List<CtorDefinition>();
            Members = new List<MemberDefinition>();
            Methods = new List<MethodDefinition>();
            StaticMethods = new List<MethodDefinition>();
            SkipDeclaration = skipDeclaration;
            InstanceDeclaration = instanceDeclaration;
        }
        
        public Type ClrType { get; }
        public string Name { get; } 
        public string Declaration { get; }
        public List<CtorDefinition> Ctors { get; }
        public List<MemberDefinition> Members { get; }
        public List<MethodDefinition> Methods { get; }
        public List<MethodDefinition> StaticMethods { get; }
        public bool SkipDeclaration { get; }
        
        public string InstanceDeclaration { get; }

        public bool HasInstanceDeclaration => InstanceDeclaration != null;
    }
}