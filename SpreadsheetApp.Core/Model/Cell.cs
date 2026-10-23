using SpreadsheetApp.Parsing;

namespace SpreadsheetApp.Model;

/// <summary>A single cell: expression text, parsed tree and computed value or error.</summary>
public sealed class Cell
{
    public Cell(CellAddress address)
    {
        Address = address;
    }

    public CellAddress Address { get; }
    public string Expression { get; set; } = string.Empty;
    public Value? Value { get; set; }
    public CalcException? Error { get; set; }
    public Expr? Ast { get; set; }
    public HashSet<CellAddress> Dependencies { get; set; } = new();

    public bool HasError => Error is not null;
}
