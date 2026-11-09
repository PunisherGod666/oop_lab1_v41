using SpreadsheetApp.Parsing;

namespace SpreadsheetApp.Model;

/// <summary>Evaluates cells in dependency order and detects reference cycles.</summary>
internal sealed class Recalculator
{
    private readonly IReadOnlyDictionary<CellAddress, Cell> _cells;
    private readonly DependencyGraph _graph;
    private readonly Func<CellAddress, bool> _isInsideTable;
    private readonly Evaluator _evaluator;

    // Cells still waiting to be evaluated and the chain being evaluated right now
    private readonly HashSet<CellAddress> _pending = new();
    private readonly List<CellAddress> _inProgress = new();

    public Recalculator(IReadOnlyDictionary<CellAddress, Cell> cells, DependencyGraph graph,
        Func<CellAddress, bool> isInsideTable)
    {
        _cells = cells;
        _graph = graph;
        _isInsideTable = isInsideTable;
        _evaluator = new Evaluator(ResolveCell);
    }

    public void RecalculateAll() => Recalculate(_cells.Keys.ToList());

    // The cell and everything that depends on it, nearest dependents first
    public void RecalculateFrom(CellAddress start) => Recalculate(_graph.GetRecalculationOrder(start));

    private void Recalculate(IReadOnlyList<CellAddress> order)
    {
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
        if (!_isInsideTable(address))
            throw new CalcException(ErrorKind.InvalidReference, $"Клітинка {address} поза межами таблиці");

        if (!_cells.TryGetValue(address, out var cell))
            throw new CalcException(ErrorKind.EmptyReference, $"Клітинка {address} порожня (null)");

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
