using System.Collections.Generic;
using System.Linq;

namespace CsToTs.TypeScript {

    public class MethodDefinition {

        public MethodDefinition(string declaration, IEnumerable<MemberDefinition> parameters = null, 
                                IEnumerable<string> lines = null, IEnumerable<string> decorators = null, string returnType = null, bool isStatic = false) {
            Declaration = declaration;
            Parameters = parameters != null ? parameters.ToList() : new List<MemberDefinition>();
            Lines = (lines as IList<string>) ?? new List<string>();
            Decorators = (decorators as IList<string>) ?? new List<string>();
            ReturnType = returnType;
            IsStatic = isStatic;
        }

        public string Declaration { get; }
        public IList<MemberDefinition> Parameters { get; }
        public IList<string> Lines { get; }
        public IList<string> Decorators { get; } 
        
        public string ReturnType { get; }
        
        public bool IsStatic { get; }
        
        public string ParameterStr 
            => string.Join(", ", Parameters.Select(m => $"{m.Name}: {m.Type}"));
    }
}