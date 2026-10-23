using System.Numerics;
using SpreadsheetApp.Model;

namespace SpreadsheetApp.Parsing;

/// <summary>Expression tree node.</summary>
public abstract class Expr
{
    protected Expr(int position)
    {
        Position = position;
    }

    // Start index in the source text, used to locate errors
    public int Position { get; }

    public abstract T Accept<T>(IExprVisitor<T> visitor);
}

public sealed class NumberLiteral : Expr
{
    public NumberLiteral(BigInteger value, int position) : base(position)
    {
        Value = value;
    }

    public BigInteger Value { get; }

    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitNumber(this);
}

public sealed class CellReference : Expr
{
    public CellReference(CellAddress address, int position) : base(position)
    {
        Address = address;
    }

    public CellAddress Address { get; }

    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitCellReference(this);
}

public sealed class UnaryExpr : Expr
{
    public UnaryExpr(UnaryOp op, Expr operand, int position) : base(position)
    {
        Operator = op;
        Operand = operand;
    }

    public UnaryOp Operator { get; }
    public Expr Operand { get; }

    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitUnary(this);
}

public sealed class BinaryExpr : Expr
{
    public BinaryExpr(BinaryOp op, Expr left, Expr right, int position) : base(position)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public BinaryOp Operator { get; }
    public Expr Left { get; }
    public Expr Right { get; }

    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitBinary(this);
}

public sealed class FunctionCall : Expr
{
    public FunctionCall(FunctionKind function, IReadOnlyList<Expr> arguments, int position) : base(position)
    {
        Function = function;
        Arguments = arguments;
    }

    public FunctionKind Function { get; }
    public IReadOnlyList<Expr> Arguments { get; }

    public override T Accept<T>(IExprVisitor<T> visitor) => visitor.VisitFunctionCall(this);
}
