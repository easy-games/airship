using System.IO;

namespace TypescriptAst
{
    public class TsKeyOfOperator : IExpression
    {
        public SyntaxKind SyntaxKind => SyntaxKind.KeyOfOperator;
        public IExpression Expression { get; set; }
        
        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write("keyof ");
            Expression.Render(renderState, writer);
        }
        
        public TsKeyOfOperator(IExpression expression)
        {
            Expression = expression;
        }
    }
}