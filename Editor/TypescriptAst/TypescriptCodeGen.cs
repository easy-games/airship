using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using Unity.VisualScripting;

namespace TypescriptAst {
    public class RenderState {
        public int Indent { get; set; }

        public string IndentString => new ('\t', Indent);
    }
    
    public enum SyntaxKind {
        Identifier,
        EnumMember,
        StringLiteral,
        EnumDeclaration,
        Comment,
        NumericLiteral,
        TypeDeclaration,
        KeyOfOperator,
        FunctionDeclaration,
        Parameter,
        ArrayType,
        Block,
        
        StringKeyword,
        NumberKeyword,
        BooleanKeyword,
        UndefinedKeyword,
        UnknownKeyword,
        VoidKeyword,
        
        TrueKeyword,
        FalseKeyword,
        
        ExportKeyword,
        DefaultKeyword,
        AbstractKeyword,
        DeclareKeyword,
        ExpressionStatement,
        TypeReference,
        FunctionType
    }

    public interface IRenderableTsNode {
        public void Render(RenderState renderState, StringWriter writer);
    }

    public interface ITypeNode : IRenderableTsNode {}

    public interface IModifier : IRenderableTsNode {}

    public interface IExpression : IRenderableTsNode {
        public SyntaxKind SyntaxKind { get; }
    }
    
    public interface IStatement : IRenderableTsNode {
        public SyntaxKind SyntaxKind { get; }
    }

    public struct TsSourceFile : IRenderableTsNode {
        public IStatement[] Statements { get; set; }

        public void Render(RenderState renderState, StringWriter writer)
        {
            foreach (var statement in Statements) {
                statement.Render(renderState, writer);
                writer.WriteLine();
            }
        }

        public override string ToString() {
            var renderState = new RenderState();
            var writer = new StringWriter();
            
            Render(renderState, writer);
            return writer.ToString();
        }
    }
}