using System.IO;

namespace TypescriptAst
{
    public class TsNumericLiteral : IExpression
    {
        public SyntaxKind SyntaxKind => SyntaxKind.NumericLiteral;
        public double Value { get; set; }
        
        public static implicit operator TsNumericLiteral(double value)
        {
            return new TsNumericLiteral(value);
        }
        
        public static implicit operator TsNumericLiteral(int value)
        {
            return new TsNumericLiteral(value);
        }
        
        public static implicit operator TsNumericLiteral(float value)
        {
            return new TsNumericLiteral(value);
        }
        
        public TsNumericLiteral(double value) {
            Value = value;
        }
        
        public TsNumericLiteral(int value) {
            Value = value;
        }
        
        public TsNumericLiteral(float value) {
            Value = value;
        }
        
        public void Render(RenderState renderState, StringWriter writer) {
            writer.Write($"{Value}");
        }
    }
}