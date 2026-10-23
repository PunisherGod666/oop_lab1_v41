using SpreadsheetApp.Model;

namespace SpreadsheetApp.Parsing;

/// <summary>Collects all cell addresses referenced by an expression.</summary>
public sealed class DependencyCollector : IExprVisitor<HashSet<CellAddress>>
{
    private HashSet<CellAddress> _result = new();

    public HashSet<CellAddress> Collect(Expr ast)
    {
        _result = new HashSet<CellAddress>();
        return ast.Accept(this);
    }

    public HashSet<CellAddress> VisitNumber(NumberLiteral node) => _result;

    public HashSet<CellAddress> VisitCellReference(CellReference node)
    {
        _result.Add(node.Address);
        return _result;
    }

    public HashSet<CellAddress> VisitUnary(UnaryExpr node) => node.Operand.Accept(this);

    public HashSet<CellAddress> VisitBinary(BinaryExpr node)
    {
        node.Left.Accept(this);
        return node.Right.Accept(this);
    }

    public HashSet<CellAddress> VisitFunctionCall(FunctionCall node)
    {
        foreach (Expr argument in node.Arguments)
            argument.Accept(this);
        return _result;
    }
}
