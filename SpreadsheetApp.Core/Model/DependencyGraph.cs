namespace SpreadsheetApp.Model;

/// <summary>Directed graph of cell references, kept in both directions.</summary>
public sealed class DependencyGraph
{
    private readonly Dictionary<CellAddress, HashSet<CellAddress>> _dependsOn = new();
    private readonly Dictionary<CellAddress, HashSet<CellAddress>> _dependents = new();

    // Replaces the set of cells that the given cell references
    public void Update(CellAddress cell, HashSet<CellAddress> dependencies)
    {
        if (_dependsOn.TryGetValue(cell, out var old))
        {
            foreach (var dependency in old)
            {
                if (_dependents.TryGetValue(dependency, out var set))
                {
                    set.Remove(cell);
                    if (set.Count == 0) _dependents.Remove(dependency);
                }
            }
        }

        if (dependencies.Count == 0)
        {
            _dependsOn.Remove(cell);
            return;
        }

        _dependsOn[cell] = new HashSet<CellAddress>(dependencies);
        foreach (var dependency in dependencies)
        {
            if (!_dependents.TryGetValue(dependency, out var set))
                _dependents[dependency] = set = new HashSet<CellAddress>();
            set.Add(cell);
        }
    }

    // True if a reference path leads from the cell back to itself
    public bool HasCycle(CellAddress start)
    {
        var visited = new HashSet<CellAddress>();
        var stack = new Stack<CellAddress>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!_dependsOn.TryGetValue(current, out var dependencies))
                continue;

            foreach (var dependency in dependencies)
            {
                if (dependency == start)
                    return true;
                if (visited.Add(dependency))
                    stack.Push(dependency);
            }
        }
        return false;
    }

    // The cell plus all its transitive dependents, nearest first (BFS)
    public IReadOnlyList<CellAddress> GetRecalculationOrder(CellAddress start)
    {
        var order = new List<CellAddress> { start };
        var visited = new HashSet<CellAddress> { start };
        var queue = new Queue<CellAddress>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_dependents.TryGetValue(current, out var dependents))
                continue;

            foreach (var dependent in dependents)
            {
                if (visited.Add(dependent))
                {
                    order.Add(dependent);
                    queue.Enqueue(dependent);
                }
            }
        }
        return order;
    }

    // Cells that reference the given cell directly
    public IReadOnlyCollection<CellAddress> GetDependents(CellAddress cell) =>
        _dependents.TryGetValue(cell, out var set) ? set : Array.Empty<CellAddress>();
}
