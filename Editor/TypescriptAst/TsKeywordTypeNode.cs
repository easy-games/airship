using System;
using System.IO;

namespace TypescriptAst {
    public class TsKeywordTypeNode : ITypeNode {
        public SyntaxKind SyntaxKind { get; }

        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write(SyntaxKind switch {
                SyntaxKind.StringKeyword => "string",
                SyntaxKind.NumberKeyword => "number",
                SyntaxKind.BooleanKeyword => "boolean",
                SyntaxKind.UndefinedKeyword => "undefined",
                SyntaxKind.UnknownKeyword => "unknown",
                SyntaxKind.VoidKeyword => "void",
                _ => throw new InvalidCastException(),
            });
        }

        private TsKeywordTypeNode(SyntaxKind syntaxKind) {
            SyntaxKind = syntaxKind;
        }

        public static TsKeywordTypeNode StringTypeNode() {
            return new TsKeywordTypeNode(SyntaxKind.StringKeyword);
        }
        
        public static TsKeywordTypeNode NumberTypeNode() {
            return new TsKeywordTypeNode(SyntaxKind.NumberKeyword);
        }
        
        public static TsKeywordTypeNode VoidTypeNode() {
            return new TsKeywordTypeNode(SyntaxKind.VoidKeyword);
        }
        
        public static TsKeywordTypeNode BooleanTypeNode() {
            return new TsKeywordTypeNode(SyntaxKind.BooleanKeyword);
        }
        
        public static TsKeywordTypeNode UndefinedTypeNode() {
            return new TsKeywordTypeNode(SyntaxKind.UndefinedKeyword);
        }
        
        public static TsKeywordTypeNode UnknownTypeNode() {
            return new TsKeywordTypeNode(SyntaxKind.UnknownKeyword);
        }
    }
}