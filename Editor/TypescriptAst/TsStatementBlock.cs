using System.IO;

namespace Editor.Typescript {
    public class TsStatementBlock : IBody {
        public SyntaxKind SyntaxKind => SyntaxKind.Block;
        public IStatement Statement { get; set; }
        public void Render(RenderState renderState, StringWriter writer) {
            if (Statement != null) {
                Statement.Render(renderState, writer);
            }
            writer.WriteLine(";");
        }
    }
}