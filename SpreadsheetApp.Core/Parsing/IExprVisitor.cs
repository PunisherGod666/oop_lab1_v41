namespace SpreadsheetApp.Parsing;

/// <summary>Visitor over the expression tree.</summary>
public interface IExprVisitor<T>
{
    T VisitNumber(NumberLiteral node);
    T VisitCellReference(CellReference node);
    T VisitUnary(UnaryExpr node);
    T VisitBinary(BinaryExpr node);
    T VisitFunctionCall(FunctionCall node);
}
