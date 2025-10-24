using System.IO;

namespace TypescriptAst
{
    public class TsTypeDeclaration : IStatement
    {
        public SyntaxKind SyntaxKind => SyntaxKind.TypeDeclaration;
        public bool Export { get; set; }
        public TsIdentifier Identifier { get; set; }
        public IExpression Expression { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write(renderState.IndentString);
            
            if (Export)
            {
                writer.Write("export ");
            }
            
            writer.Write("type ");
            Identifier.Render(renderState, writer);
            
            writer.Write(" = ");
            
            Expression.Render(renderState, writer);
            writer.WriteLine(";");
        }
    }
}