using System;
using System.IO;

namespace TypescriptAst {
    public class TsKeywordModifier : IModifier {
        public SyntaxKind SyntaxKind { get; }

        private TsKeywordModifier(SyntaxKind syntaxKind) {
            SyntaxKind = syntaxKind;
        }
        
        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write(SyntaxKind switch {
                SyntaxKind.DefaultKeyword => "default",
                SyntaxKind.ExportKeyword => "export",
                SyntaxKind.DeclareKeyword => "declare",
                SyntaxKind.AbstractKeyword => "abstract",
                _ => throw new InvalidCastException(),
            });
        }
        
        public static TsKeywordModifier DefaultModifier() => new TsKeywordModifier(SyntaxKind.DefaultKeyword);
        public static TsKeywordModifier ExportModifier() => new TsKeywordModifier(SyntaxKind.ExportKeyword);
        public static TsKeywordModifier DeclareModifier() => new TsKeywordModifier(SyntaxKind.DeclareKeyword);
        public static TsKeywordModifier AsbtractModifier() => new TsKeywordModifier(SyntaxKind.AbstractKeyword);
    }
}