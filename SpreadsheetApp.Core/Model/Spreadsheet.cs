using SpreadsheetApp.Parsing;

namespace SpreadsheetApp.Model;

/// <summary>Spreadsheet model: stores non-empty cells and recalculates dependents on every change.</summary>
public sealed class Spreadsheet
{
    public const int DefaultRows = 10;
    public const int DefaultColumns = 10;

    private readonly Dictionary<CellAddress, Cell> _cells = new();
    private readonly DependencyGraph _graph = new();
    private readonly Evaluator _evaluator;

    // Recalculation state: cells waiting to be evaluated and the chain currently being evaluated
    private readonly HashSet<CellAddress> _pending = new();
    private readonly List<CellAddress> _inProgress = new();

    public Spreadsheet(int rows = DefaultRows, int columns = DefaultColumns)
    {
        if (rows < 1) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
        RowCount = rows;
        ColumnCount = columns;
        _evaluator = new Evaluator(ResolveCell);
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
            RecalculateFrom(address);
            return;
        }

        if (!_cells.TryGetValue(address, out var cell))
            _cells[address] = cell = new Cell(address);

        cell.Expression = expression;
        try
        {
            var ast = FormulaParser.Parse(expression);
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
        RecalculateFrom(address);
    }

    public bool HasCycle(CellAddress address) => _graph.HasCycle(address);

    // Rows and columns are added and removed at the end of the table
    public void AddRow()
    {
        RowCount++;
        RecalculateAll();
    }

    public void RemoveRow()
    {
        if (RowCount <= 1)
            throw new InvalidOperationException("Таблиця повинна містити хоча б один рядок");
        RowCount--;
        RemoveCellsOutsideBounds();
        RecalculateAll();
    }

    public void AddColumn()
    {
        ColumnCount++;
        RecalculateAll();
    }

    public void RemoveColumn()
    {
        if (ColumnCount <= 1)
            throw new InvalidOperationException("Таблиця повинна містити хоча б один стовпець");
        ColumnCount--;
        RemoveCellsOutsideBounds();
        RecalculateAll();
    }

    // True if the row/column has data or is referenced by other cells
    public bool IsRowInUse(int row) =>
        Enumerable.Range(0, ColumnCount).Any(column => IsInUse(new CellAddress(row, column)));

    public bool IsColumnInUse(int column) =>
        Enumerable.Range(0, RowCount).Any(row => IsInUse(new CellAddress(row, column)));

    private bool IsInUse(CellAddress address) =>
        _cells.ContainsKey(address) || _graph.GetDependents(address).Count > 0;

    public void RecalculateAll()
    {
        _pending.Clear();
        _pending.UnionWith(_cells.Keys);
        foreach (var address in _cells.Keys.ToList())
        {
            if (_pending.Contains(address))
                EvaluateCell(_cells[address]);
        }
        _pending.Clear();
    }

    // Recalculates the cell and everything that depends on it
    public void RecalculateFrom(CellAddress start)
    {
        var order = _graph.GetRecalculationOrder(start);
        _pending.Clear();
        foreach (var address in order)
        {
            if (_cells.ContainsKey(address))
                _pending.Add(address);
        }

        foreach (var address in order)
        {
            if (_pending.Contains(address))
                EvaluateCell(_cells[address]);
        }
        _pending.Clear();
    }

    private void RemoveCellsOutsideBounds()
    {
        foreach (var address in _cells.Keys.Where(a => !Contains(a)).ToList())
        {
            _cells.Remove(address);
            _graph.Update(address, new HashSet<CellAddress>());
        }
    }

    // Evaluates a cell; pending referenced cells are evaluated recursively, re-entry means a cycle
    private void EvaluateCell(Cell cell)
    {
        int cycleStart = _inProgress.IndexOf(cell.Address);
        if (cycleStart >= 0)
        {
            var path = _inProgress.Skip(cycleStart).Append(cell.Address);
            throw new CalcException(ErrorKind.CyclicReference,
                "Циклічне посилання: " + string.Join(" → ", path));
        }

        _pending.Remove(cell.Address);
        if (cell.Ast is null)
            return; // syntax error is already stored

        _inProgress.Add(cell.Address);
        try
        {
            cell.Value = _evaluator.Evaluate(cell.Ast);
            cell.Error = null;
        }
        catch (CalcException ex)
        {
            cell.Value = null;
            cell.Error = ex;
        }
        finally
        {
            _inProgress.RemoveAt(_inProgress.Count - 1);
        }
    }

    // Value lookup used by the evaluator for cell references
    private Value ResolveCell(CellAddress address)
    {
        if (!Contains(address))
            throw new CalcException(ErrorKind.InvalidReference, $"Клітинка {address} поза межами таблиці");

        if (!_cells.TryGetValue(address, out var cell))
            return Value.FromNumber(0); // empty cell counts as zero

        if (_pending.Contains(address) || _inProgress.Contains(address))
            EvaluateCell(cell);

        if (cell.Error is { } error)
        {
            // Errors propagate; cycle members share the same cycle message
            string message = error.Kind == ErrorKind.CyclicReference
                ? error.Message
                : $"Клітинка {address} містить помилку ({error.Code})";
            throw new CalcException(error.Kind, message);
        }

        return cell.Value ?? Value.FromNumber(0);
    }
}
