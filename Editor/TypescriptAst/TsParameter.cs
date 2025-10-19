using System.IO;

namespace Editor.Typescript {
    public class TsParameter : IRenderableTsNode {
        public SyntaxKind SyntaxKind => SyntaxKind.Parameter;
        public TsIdentifier Name { get; set; }
        public ITypeNode Type { get; set; }
        public IExpression Initializer { get; set; }

        public bool DotDotDot { get; set; }

        public void Render(RenderState renderState, StringWriter writer) {
            if (DotDotDot) {
                writer.Write("...");
            }
            
            if (Name != null) {
                Name.Render(renderState, writer);
            }
            
            if (Type != null) {
                if (Name != null) writer.Write(": ");
                Type.Render(renderState, writer);
            }
            
            if (Initializer != null) {
                writer.Write(" = ");
                Initializer.Render(renderState, writer);
            }
        }

        public TsParameter(TsIdentifier name, ITypeNode type, IExpression initializer) {
            Name = name;
            Type = type;
            Initializer = initializer;
        }
        
        public TsParameter(TsIdentifier name, ITypeNode type) {
            Name = name;
            Type = type;
        }

        public TsParameter(TsIdentifier name, IExpression initializer) {
            Name = name;
            Initializer = initializer;
        }
    }
}