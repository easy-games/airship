using System.Collections.Generic;
using System.Linq;

namespace CsToTs.TypeScript {

    public class CtorDefinition {

        public CtorDefinition(IEnumerable<string> lines, IEnumerable<MemberDefinition> parameters, string returnType) {
            Lines = (lines?.ToList() ?? new List<string>()).AsReadOnly();
            Parameters = parameters;
            ReturnType = returnType;
        }
        
        public IEnumerable<MemberDefinition> Parameters { get; }
        
        public string CtorParameterStr 
            => string.Join(", ", Parameters.Select(m => $"{m.Name}: {m.Type}"));

        public string ReturnType { get; }
        
        public IReadOnlyCollection<string> Lines { get; }
    }
}