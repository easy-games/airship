using System.IO;

namespace TypescriptAst
{
    public class TsComment : IStatement {
        public SyntaxKind SyntaxKind => SyntaxKind.Comment;
        public string Text { get; set; }

        public bool Multiline { get; set; }
        public bool IsJsDoc { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            if (Multiline) {
                writer.WriteLine(renderState.IndentString + (IsJsDoc ? $"/**" : $"/*"));
                renderState.Indent++;

                if (IsJsDoc)
                {
                    writer.Write(new string('\t', renderState.Indent - 1) + " *\t" + Text);
                }
                else
                {
                    writer.Write(renderState.IndentString + Text);
                }
                
               
                writer.WriteLine();
                renderState.Indent--;
                writer.Write(renderState.IndentString + (IsJsDoc ? $" */" : $"*/"));
            }
            else {
                if (IsJsDoc)
                {
                    writer.Write(renderState.IndentString + $"/** {Text} */");
                }
                else
                {
                    writer.Write(renderState.IndentString + $"// {Text}");
                }
            }
        }
    }
}