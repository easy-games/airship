using System.IO;

namespace TypescriptAst {
    public class TsArrayTypeNode : ITypeNode {
        public SyntaxKind SyntaxKind => SyntaxKind.ArrayType;
        public ITypeNode ElementType { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            if (ElementType != null) {
                ElementType.Render(renderState, writer);
            } else {
                writer.Write("unknown");
            }
           
            writer.Write("[]");
        }

        public TsArrayTypeNode(ITypeNode elementType) {
            ElementType = elementType;
        }
    }

    public class TsTypeReferenceNode : ITypeNode {
        public SyntaxKind SyntaxKind => SyntaxKind.TypeReference;
        public TsIdentifier TypeName { get; set; }
        public ITypeNode[] TypeArguments { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            TypeName.Render(renderState, writer);

            if (TypeArguments != null) {
                writer.Write("<");
                for (var i = 0; i < TypeArguments.Length; i++) {
                    TypeArguments[i].Render(renderState, writer);
                    if (i < TypeArguments.Length - 1) writer.Write(", ");
                }
                writer.Write(">");
            }
        }

        public TsTypeReferenceNode(TsIdentifier typeName, ITypeNode[] typeArguments) {
            TypeName = typeName;
            TypeArguments = typeArguments;
        }
        
        public TsTypeReferenceNode(TsIdentifier typeName) {
            TypeName = typeName;
        }
    }

    public class TsFunctionType : ITypeNode {
        public SyntaxKind SyntaxKind => SyntaxKind.FunctionType;

        public ITypeNode Type { get; set; }
        public TsParameter[] Parameters { get; set; }

        /*
         * typeParameters:undefined
                parameters:[]
                type: VoidKeyword
         */
        
        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write("(");

            if (Parameters != null) {
                for (var i = 0; i < Parameters.Length; i++) {
                    Parameters[i].Render(renderState, writer);
                    if (i < Parameters.Length - 1) writer.Write(", ");
                }
            }
            
            writer.Write(")");
            writer.Write(" => ");
            if (Type != null) {
                Type.Render(renderState, writer);
            } else {
                writer.Write("any");
            }
        }

        public TsFunctionType(TsParameter[] parameters, ITypeNode returnType) {
            Type = returnType;
            Parameters = parameters;
        }
    }
}