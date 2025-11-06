using System.IO;

namespace TypescriptAst
{
    public class TsIdentifier : IExpression {
        public SyntaxKind SyntaxKind => SyntaxKind.Identifier;
        public string Name { get; set; }
        public TsIdentifier(string name) {
            Name = name;
        }

        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write(Name);
        }

        public static implicit operator TsIdentifier(string name) {
            return new TsIdentifier(name);
        }
    }
}