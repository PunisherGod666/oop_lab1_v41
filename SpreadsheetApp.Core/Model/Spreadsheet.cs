using SpreadsheetApp.Parsing;

namespace SpreadsheetApp.Model;

/// <summary>Spreadsheet model: table size, non-empty cells and their dependencies.</summary>
public sealed class Spreadsheet
{
    public const int DefaultRows = 10;
    public const int DefaultColumns = 10;

    private readonly Dictionary<CellAddress, Cell> _cells = new();
    private readonly DependencyGraph _graph = new();
    private readonly IFormulaParser _parser;
    private readonly Recalculator _recalculator;

    public Spreadsheet(int rows = DefaultRows, int columns = DefaultColumns, IFormulaParser? parser = null)
    {
        if (rows < 1) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
        RowCount = rows;
        ColumnCount = columns;
        _parser = parser ?? new FormulaParser();
        _recalculator = new Recalculator(_cells, _graph, Contains);
    }

    public int RowCount { get; private set; }
    public int ColumnCount { get; private set; }

    public IEnumerable<Cell> Cells => _cells.Values;

    public bool Contains(CellAddress address) => address.Row < RowCount && address.Column < ColumnCount;

    // Empty cells are returned as a fresh object that is not stored
    public Cell GetCell(CellAddress address) =>
        _cells.TryGetValue(address, out var cell) ? cell : new Cell(address);

    // Parses the expression, updates the dependency graph and recalculates dependents
    public void SetExpression(CellAddress address, string expression)
    {
        if (!Contains(address))
            throw new ArgumentOutOfRangeException(nameof(address), $"Клітинка {address} поза межами таблиці");

        expression = expression?.Trim() ?? string.Empty;
        if (expression.Length == 0)
        {
            _cells.Remove(address);
            _graph.Update(address, new HashSet<CellAddress>());
            _recalculator.RecalculateFrom(address);
            return;
        }

        if (!_cells.TryGetValue(address, out var cell))
            _cells[address] = cell = new Cell(address);

        cell.Expression = expression;
        try
        {
            var ast = _parser.Parse(expression);
            cell.Ast = ast;
            cell.Dependencies = new DependencyCollector().Collect(ast);
            cell.Error = null;
        }
        catch (CalcException ex)
        {
            cell.Ast = null;
            cell.Dependencies = new HashSet<CellAddress>();
            cell.Value = null;
            cell.Error = ex;
        }

        _graph.Update(address, cell.Dependencies);
        _recalculator.RecalculateFrom(address);
    }

    public bool HasCycle(CellAddress address) => _graph.HasCycle(address);

    // Rows and columns are added and removed at the end of the table
    public void AddRow()
    {
        RowCount++;
        _recalculator.RecalculateAll();
    }

    public void RemoveRow()
    {
        if (RowCount <= 1)
            throw new InvalidOperationException("Таблиця повинна містити хоча б один рядок");
        RowCount--;
        RemoveCellsOutsideBounds();
        _recalculator.RecalculateAll();
    }

    public void AddColumn()
    {
        ColumnCount++;
        _recalculator.RecalculateAll();
    }

    public void RemoveColumn()
    {
        if (ColumnCount <= 1)
            throw new InvalidOperationException("Таблиця повинна містити хоча б один стовпець");
        ColumnCount--;
        RemoveCellsOutsideBounds();
        _recalculator.RecalculateAll();
    }

    // True if the row/column has data or is referenced by other cells
    public bool IsRowInUse(int row) =>
        Enumerable.Range(0, ColumnCount).Any(column => IsInUse(new CellAddress(row, column)));

    public bool IsColumnInUse(int column) =>
        Enumerable.Range(0, RowCount).Any(row => IsInUse(new CellAddress(row, column)));

    private bool IsInUse(CellAddress address) =>
        _cells.ContainsKey(address) || _graph.GetDependents(address).Count > 0;

    private void RemoveCellsOutsideBounds()
    {
        foreach (var address in _cells.Keys.Where(a => !Contains(a)).ToList())
        {
            _cells.Remove(address);
            _graph.Update(address, new HashSet<CellAddress>());
        }
    }
}
