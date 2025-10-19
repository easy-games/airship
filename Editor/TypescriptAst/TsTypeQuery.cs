using System.IO;

namespace Editor.Typescript
{
    public class TsTypeQuery : IExpression
    {
        public SyntaxKind SyntaxKind => SyntaxKind.Identifier;
        public IExpression Expression { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write("typeof ");
            Expression.Render(renderState, writer);
        }

        public TsTypeQuery(IExpression expression)
        {
            Expression = expression;
        }
    }
}