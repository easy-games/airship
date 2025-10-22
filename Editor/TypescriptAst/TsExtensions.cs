namespace TypescriptAst {
    public static class TsExtensions {
        public static TsBlock ToBlock(this IStatement[] statements) {
            return new TsBlock() { Statements = statements };
        }

        public static TsStatementBlock ToBlock(this IStatement statement) {
            return new TsStatementBlock() { Statement = statement };
        }

        public static TsExpressionStatement ToExpressionStatement(this IExpression expression) {
            return new TsExpressionStatement() { Expression = expression };
        }
    }
}