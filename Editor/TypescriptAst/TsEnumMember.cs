using System.IO;

namespace Editor.Typescript
{
    public class TsEnumMember : IExpression {
        public SyntaxKind SyntaxKind => SyntaxKind.EnumMember;
        
        public TsPropertyName Name { get; set; }
        public IExpression Initializer { get; set; }

        public TsComment Comment { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            if (Comment != null)
            {
                Comment.Render(renderState, writer);
                writer.WriteLine();
            }
            
            writer.Write(renderState.IndentString);

            if (Initializer != null) {
                Name.Render(renderState, writer);
                writer.Write(" = ");
                Initializer.Render(renderState, writer);
            }
            else {
                Name.Render(renderState, writer);
            }
        }

        public TsEnumMember(TsPropertyName propertyName)
        {
            Name = propertyName;
            Initializer = null;
            Comment = null;
        }
        
        public TsEnumMember(TsPropertyName propertyName, IExpression expression)
        {
            Name = propertyName;
            Initializer = expression;
            Comment = null;
        }
    }
}