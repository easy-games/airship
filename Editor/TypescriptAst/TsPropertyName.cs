using System.IO;

namespace Editor.Typescript
{
    public class TsPropertyName : IRenderableTsNode {
        private IExpression Expression { get; set; }

        public static implicit operator TsPropertyName(string value)
        {
            return new TsPropertyName(value);
        }

        public TsPropertyName(string propertyName)
        {
            Expression = new TsStringLiteral(propertyName);
        }

        public TsPropertyName(TsStringLiteral literal) {
            Expression = literal;
        }
        
        public TsPropertyName(TsIdentifier id) {
            Expression = id;
        }
        
        public void Render(RenderState renderState, StringWriter writer) {
            if (Expression is TsStringLiteral stringLiteral) {
                if (stringLiteral.Text.Contains(" ") || stringLiteral.Text == "") {
                    writer.Write("[");
                    stringLiteral.Render(renderState, writer);
                    writer.Write("]");
                }
                else {
                    writer.Write(stringLiteral.Text);
                }
            }
            else {
                Expression.Render(renderState, writer);
            }
        }
    }
}