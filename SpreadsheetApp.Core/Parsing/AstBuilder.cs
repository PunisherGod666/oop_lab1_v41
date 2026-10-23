using System.Numerics;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using SpreadsheetApp.Model;
using SpreadsheetApp.Parsing.Grammar;

namespace SpreadsheetApp.Parsing;

/// <summary>Converts the ANTLR parse tree into an Expr tree and validates functions and cell addresses.</summary>
internal sealed class AstBuilder : CellFormulaBaseVisitor<Expr>
{
    // Supported functions with their argument count limits
    private static readonly Dictionary<string, (FunctionKind Kind, int MinArgs, int MaxArgs)> Functions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["inc"] = (FunctionKind.Inc, 1, 1),
            ["dec"] = (FunctionKind.Dec, 1, 1),
            ["max"] = (FunctionKind.Max, 2, 2),
            ["min"] = (FunctionKind.Min, 2, 2),
            ["mmax"] = (FunctionKind.MMax, 1, int.MaxValue),
            ["mmin"] = (FunctionKind.MMin, 1, int.MaxValue),
        };

    public override Expr VisitFormula(CellFormulaParser.FormulaContext context) =>
        Visit(context.eqvExpr());

    public override Expr VisitEqvExpr(CellFormulaParser.EqvExprContext context) =>
        FoldLeft(context.orExpr(), context.EQV(), _ => BinaryOp.Eqv);

    public override Expr VisitOrExpr(CellFormulaParser.OrExprContext context) =>
        FoldLeft(context.andExpr(), context.OR(), _ => BinaryOp.Or);

    public override Expr VisitAndExpr(CellFormulaParser.AndExprContext context) =>
        FoldLeft(context.notExpr(), context.AND(), _ => BinaryOp.And);

    public override Expr VisitLogicalNot(CellFormulaParser.LogicalNotContext context) =>
        new UnaryExpr(UnaryOp.Not, Visit(context.notExpr()), context.NOT().Symbol.StartIndex);

    public override Expr VisitPlainComparison(CellFormulaParser.PlainComparisonContext context) =>
        Visit(context.comparison());

    public override Expr VisitComparison(CellFormulaParser.ComparisonContext context) =>
        FoldLeft(context.additive(), context._op, type => type switch
        {
            CellFormulaLexer.EQ => BinaryOp.Equal,
            CellFormulaLexer.NEQ => BinaryOp.NotEqual,
            CellFormulaLexer.LT => BinaryOp.Less,
            CellFormulaLexer.LE => BinaryOp.LessOrEqual,
            CellFormulaLexer.GT => BinaryOp.Greater,
            _ => BinaryOp.GreaterOrEqual,
        });

    public override Expr VisitAdditive(CellFormulaParser.AdditiveContext context) =>
        FoldLeft(context.multiplicative(), context._op,
            type => type == CellFormulaLexer.PLUS ? BinaryOp.Add : BinaryOp.Subtract);

    public override Expr VisitMultiplicative(CellFormulaParser.MultiplicativeContext context) =>
        FoldLeft(context.unary(), context._op, type => type switch
        {
            CellFormulaLexer.MUL => BinaryOp.Multiply,
            CellFormulaLexer.SLASH => BinaryOp.Divide,
            CellFormulaLexer.DIV => BinaryOp.IntDivide,
            _ => BinaryOp.Modulo,
        });

    public override Expr VisitSignedUnary(CellFormulaParser.SignedUnaryContext context)
    {
        var op = context.sign.Type == CellFormulaLexer.PLUS ? UnaryOp.Plus : UnaryOp.Minus;
        return new UnaryExpr(op, Visit(context.unary()), context.sign.StartIndex);
    }

    public override Expr VisitPlainPower(CellFormulaParser.PlainPowerContext context) =>
        Visit(context.power());

    public override Expr VisitPower(CellFormulaParser.PowerContext context)
    {
        Expr value = Visit(context.primary());
        if (context.CARET() is not { } caret)
            return value;
        // Right side is unary: 2^3^2 = 2^(3^2), and 2^-1 parses
        return new BinaryExpr(BinaryOp.Power, value, Visit(context.unary()), caret.Symbol.StartIndex);
    }

    public override Expr VisitNumberAtom(CellFormulaParser.NumberAtomContext context)
    {
        IToken token = context.NUMBER().Symbol;
        return new NumberLiteral(BigInteger.Parse(token.Text), token.StartIndex);
    }

    public override Expr VisitCellAtom(CellFormulaParser.CellAtomContext context)
    {
        IToken token = context.CELL().Symbol;
        if (!CellAddress.TryParse(token.Text, out var address))
            throw new CalcException(ErrorKind.InvalidReference,
                $"Некоректна адреса клітинки «{token.Text}»", token.StartIndex);
        return new CellReference(address, token.StartIndex);
    }

    public override Expr VisitFunctionAtom(CellFormulaParser.FunctionAtomContext context)
    {
        IToken name = context.IDENT().Symbol;
        if (!Functions.TryGetValue(name.Text, out var function))
            throw new CalcException(ErrorKind.Syntax, $"Невідома функція «{name.Text}»", name.StartIndex);

        var arguments = context.eqvExpr().Select(Visit).ToList();
        if (arguments.Count < function.MinArgs || arguments.Count > function.MaxArgs)
        {
            string expected = function.MinArgs == function.MaxArgs
                ? function.MinArgs.ToString()
                : $"не менше {function.MinArgs}";
            throw new CalcException(ErrorKind.Syntax,
                $"Функція {name.Text.ToLowerInvariant()} очікує {expected} аргумент(и), отримано {arguments.Count}",
                name.StartIndex);
        }

        return new FunctionCall(function.Kind, arguments, name.StartIndex);
    }

    public override Expr VisitParenAtom(CellFormulaParser.ParenAtomContext context) =>
        Visit(context.eqvExpr());

    // Folds "a op b op c" into a left-associative tree ((a op b) op c)
    private Expr FoldLeft(IReadOnlyList<ParserRuleContext> operands, IReadOnlyList<ITerminalNode> operators,
        Func<int, BinaryOp> toOp) =>
        FoldLeft(operands, operators.Select(t => t.Symbol).ToList(), toOp);

    private Expr FoldLeft(IReadOnlyList<ParserRuleContext> operands, IList<IToken> operators, Func<int, BinaryOp> toOp)
    {
        Expr result = Visit(operands[0]);
        for (int i = 1; i < operands.Count; i++)
        {
            IToken op = operators[i - 1];
            result = new BinaryExpr(toOp(op.Type), result, Visit(operands[i]), op.StartIndex);
        }
        return result;
    }
}
