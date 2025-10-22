using System.IO;

namespace TypescriptAst {
    public class TsExpressionStatement : IStatement {
        public SyntaxKind SyntaxKind => SyntaxKind.ExpressionStatement;
        public IExpression Expression { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            Expression.Render(renderState, writer);
        }
    }
}