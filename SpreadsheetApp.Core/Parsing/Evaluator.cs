using System.Numerics;
using SpreadsheetApp.Model;

namespace SpreadsheetApp.Parsing;

/// <summary>Computes the value of an Expr tree; referenced cells are resolved through a delegate.</summary>
public sealed class Evaluator : IExprVisitor<Value>
{
    // Guards against huge powers like 2^999999999 freezing the app
    public const int MaxExponent = 100_000;

    private readonly Func<CellAddress, Value> _resolveCell;

    public Evaluator(Func<CellAddress, Value> resolveCell)
    {
        _resolveCell = resolveCell;
    }

    public Value Evaluate(Expr ast) => ast.Accept(this);

    public Value VisitNumber(NumberLiteral node) => Value.FromNumber(node.Value);

    public Value VisitCellReference(CellReference node)
    {
        try
        {
            return _resolveCell(node.Address);
        }
        catch (CalcException ex) when (ex.Position < 0)
        {
            // Attach the error to the reference's position
            throw new CalcException(ex.Kind, ex.Message, node.Position);
        }
    }

    public Value VisitUnary(UnaryExpr node)
    {
        Value operand = node.Operand.Accept(this);
        return node.Operator switch
        {
            UnaryOp.Plus => Value.FromNumber(RequireNumber(operand, node, "+")),
            UnaryOp.Minus => Value.FromNumber(-RequireNumber(operand, node, "-")),
            UnaryOp.Not => Value.FromBool(!RequireBool(operand, node, "not")),
            _ => throw new InvalidOperationException($"Невідома унарна операція {node.Operator}")
        };
    }

    public Value VisitBinary(BinaryExpr node)
    {
        Value left = node.Left.Accept(this);
        Value right = node.Right.Accept(this);

        switch (node.Operator)
        {
            // Arithmetic (integer division truncates)
            case BinaryOp.Add:
                return Value.FromNumber(Num(left, "+") + Num(right, "+"));
            case BinaryOp.Subtract:
                return Value.FromNumber(Num(left, "-") - Num(right, "-"));
            case BinaryOp.Multiply:
                return Value.FromNumber(Num(left, "*") * Num(right, "*"));
            case BinaryOp.Divide:
            case BinaryOp.IntDivide:
            {
                string symbol = node.Operator == BinaryOp.Divide ? "/" : "div";
                BigInteger divisor = Num(right, symbol);
                BigInteger dividend = Num(left, symbol);
                if (divisor.IsZero)
                    throw new CalcException(ErrorKind.DivisionByZero, "Ділення на нуль", node.Position);
                return Value.FromNumber(BigInteger.Divide(dividend, divisor));
            }
            case BinaryOp.Modulo:
            {
                BigInteger divisor = Num(right, "mod");
                BigInteger dividend = Num(left, "mod");
                if (divisor.IsZero)
                    throw new CalcException(ErrorKind.DivisionByZero, "Ділення на нуль в операції mod", node.Position);
                return Value.FromNumber(BigInteger.Remainder(dividend, divisor));
            }
            case BinaryOp.Power:
                return Value.FromNumber(Power(Num(left, "^"), Num(right, "^"), node));

            // Comparison
            case BinaryOp.Equal:
                return Value.FromBool(AreEqual(left, right, node, "="));
            case BinaryOp.NotEqual:
                return Value.FromBool(!AreEqual(left, right, node, "<>"));
            case BinaryOp.Less:
                return Value.FromBool(Num(left, "<") < Num(right, "<"));
            case BinaryOp.LessOrEqual:
                return Value.FromBool(Num(left, "<=") <= Num(right, "<="));
            case BinaryOp.Greater:
                return Value.FromBool(Num(left, ">") > Num(right, ">"));
            case BinaryOp.GreaterOrEqual:
                return Value.FromBool(Num(left, ">=") >= Num(right, ">="));

            // Logic
            case BinaryOp.And:
                return Value.FromBool(Bool(left, "and") & Bool(right, "and"));
            case BinaryOp.Or:
                return Value.FromBool(Bool(left, "or") | Bool(right, "or"));
            case BinaryOp.Eqv:
                return Value.FromBool(Bool(left, "eqv") == Bool(right, "eqv"));

            default:
                throw new InvalidOperationException($"Невідома бінарна операція {node.Operator}");
        }

        BigInteger Num(Value v, string op) => RequireNumber(v, node, op);
        bool Bool(Value v, string op) => RequireBool(v, node, op);
    }

    public Value VisitFunctionCall(FunctionCall node)
    {
        string name = node.Function.ToString().ToLowerInvariant();
        var args = new List<BigInteger>(node.Arguments.Count);
        foreach (Expr argument in node.Arguments)
            args.Add(RequireNumber(argument.Accept(this), argument, name));

        BigInteger result = node.Function switch
        {
            FunctionKind.Inc => args[0] + 1,
            FunctionKind.Dec => args[0] - 1,
            FunctionKind.Max => BigInteger.Max(args[0], args[1]),
            FunctionKind.Min => BigInteger.Min(args[0], args[1]),
            FunctionKind.MMax => args.Aggregate(BigInteger.Max),
            FunctionKind.MMin => args.Aggregate(BigInteger.Min),
            _ => throw new InvalidOperationException($"Невідома функція {node.Function}")
        };
        return Value.FromNumber(result);
    }

    private static BigInteger Power(BigInteger value, BigInteger exponent, Expr node)
    {
        if (exponent.Sign < 0)
        {
            // Integer result exists only for bases 1 and -1
            if (value.IsOne) return BigInteger.One;
            if (value == BigInteger.MinusOne) return exponent.IsEven ? BigInteger.One : BigInteger.MinusOne;
            if (value.IsZero)
                throw new CalcException(ErrorKind.DivisionByZero, "Нуль у від'ємному степені", node.Position);
            throw new CalcException(ErrorKind.InvalidArgument,
                "Від'ємний показник степеня дає нецілий результат", node.Position);
        }

        if (value.IsZero || value.IsOne) return value;
        if (value == BigInteger.MinusOne) return exponent.IsEven ? BigInteger.One : BigInteger.MinusOne;
        if (exponent > MaxExponent)
            throw new CalcException(ErrorKind.InvalidArgument,
                $"Занадто великий показник степеня (максимум {MaxExponent})", node.Position);

        return BigInteger.Pow(value, (int)exponent);
    }

    // Type checks shared by all operations
    private static bool AreEqual(Value left, Value right, Expr node, string op)
    {
        if (left.Kind != right.Kind)
            throw new CalcException(ErrorKind.TypeMismatch,
                $"Операція «{op}» порівнює значення різних типів ({Describe(left)} і {Describe(right)})", node.Position);
        return left.Kind == ValueKind.Number ? left.Number == right.Number : left.Boolean == right.Boolean;
    }

    private static BigInteger RequireNumber(Value value, Expr node, string op)
    {
        if (value.Kind != ValueKind.Number)
            throw new CalcException(ErrorKind.TypeMismatch,
                $"Операція «{op}» очікує число, а отримано {Describe(value)}", node.Position);
        return value.Number;
    }

    private static bool RequireBool(Value value, Expr node, string op)
    {
        if (value.Kind != ValueKind.Boolean)
            throw new CalcException(ErrorKind.TypeMismatch,
                $"Операція «{op}» очікує логічне значення, а отримано {Describe(value)}", node.Position);
        return value.Boolean;
    }

    private static string Describe(Value value) =>
        value.Kind == ValueKind.Number ? "число" : "логічне значення";
}
