using System.IO;

namespace TypescriptAst
{
    public class TsStringLiteral : IExpression {
        public SyntaxKind SyntaxKind => SyntaxKind.StringLiteral;
        public string Text { get; set; }
        
        public static implicit operator TsStringLiteral(string value)
        {
            return new TsStringLiteral(value);
        }

        public TsStringLiteral(string text) {
            Text = text;
        }
        
        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write($"\"{Text}\"");
        }
    }
}