using System.IO;

namespace TypescriptAst {
    public interface IBody : IRenderableTsNode {}

    public class TsFunctionDeclaration : IStatement {
        public SyntaxKind SyntaxKind => SyntaxKind.FunctionDeclaration;

        public IModifier[] Modifiers { get; set; }
        public TsIdentifier Name { get; set; }
        public TsParameter[] Parameters { get; set; }

        public ITypeNode ReturnType { get; set; }
        public IBody Body { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write(renderState.IndentString);

            var modifiers = Modifiers;
            if (modifiers != null) {
                foreach (var modifer in modifiers) {
                    modifer.Render(renderState, writer);
                    writer.Write(" ");
                }
            }


            writer.Write("function");

            if (Name != null) {
                writer.Write(" ");
                Name.Render(renderState, writer);
            }

            writer.Write("(");

            if (Parameters != null) {
                for (var i = 0; i < Parameters.Length; i++) {
                    Parameters[i].Render(renderState, writer);
                    if (i < Parameters.Length - 1) writer.Write(", ");
                }
            }

            writer.Write(")");

            if (ReturnType != null) {
                writer.Write(": ");
                ReturnType.Render(renderState, writer);
            }

            if (Body != null) {
                if (Body is TsBlock block) {
                    writer.WriteLine(" {");
                    
                    foreach (var statement in block.Statements) {
                        renderState.Indent += 1;
                        statement.Render(renderState, writer);
                        writer.WriteLine();
                        renderState.Indent -= 1;
                    }
                
                    writer.Write(renderState.IndentString + "}");
                } else {
                    Body.Render(renderState, writer);
                }
            } else {
                writer.Write(";");
            }
        }

        public TsFunctionDeclaration(): this(null, null, null) {}
        public TsFunctionDeclaration(TsIdentifier name, TsParameter[] parameters, ITypeNode returnType): this(name, null, parameters, returnType) {}
        public TsFunctionDeclaration(TsIdentifier name, IModifier[] modifiers, TsParameter[] parameters, ITypeNode returnType) {
            Modifiers = modifiers;
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
        }
    }
}