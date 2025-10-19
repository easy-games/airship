using System.IO;

namespace Editor.Typescript {
    public class TsBlock : IBody, IStatement {
        public SyntaxKind SyntaxKind => SyntaxKind.Block;
        public IStatement[] Statements { get; set; }
        public void Render(RenderState renderState, StringWriter writer) {
            if (Statements != null) {
                writer.WriteLine(renderState.IndentString + "{");
                foreach (var statement in Statements) {
                    renderState.Indent += 1;
                    statement.Render(renderState, writer);
                    writer.WriteLine();
                    renderState.Indent -= 1;
                }
                writer.WriteLine(renderState.IndentString + "}");
            }
        }
    }
}