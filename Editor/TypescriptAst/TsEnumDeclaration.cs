using System.IO;

namespace Editor.Typescript
{
    public class TsEnumDeclaration : IStatement {
        public SyntaxKind SyntaxKind => SyntaxKind.EnumDeclaration;

        public bool Export { get; set; }

        public bool Const { get; set; }

        public TsComment Comment { get; set; }
        
        public TsIdentifier Identifier { get; set; }
        public TsEnumMember[] Members { get; set; }
        
        public void Render(RenderState renderState, StringWriter writer) {
            if (Comment != null)
            {
                writer.WriteLine();
                Comment.Render(renderState, writer);
                writer.WriteLine();
            }
            
            writer.Write(renderState.IndentString);

            if (Export) {
                writer.Write("export ");
            }

            if (Const) {
                writer.Write("const ");
            }
            
            writer.Write($"enum ");
            Identifier.Render(renderState, writer);
            writer.WriteLine(" {");

            if (Members != null) {
                renderState.Indent += 1;
                foreach (var member in Members) {
                    member.Render(renderState, writer);
                    writer.WriteLine(",");
                }
                renderState.Indent -= 1;
            }
            
            writer.Write(renderState.IndentString + "}");
        }
    }
}