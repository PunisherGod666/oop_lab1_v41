using System.Numerics;

namespace SpreadsheetApp.Parsing;

/// <summary>A function callable from formulas: name, allowed argument count and implementation.</summary>
public sealed class FunctionDefinition
{
    public FunctionDefinition(string name, int minArgs, int maxArgs, Func<IReadOnlyList<BigInteger>, BigInteger> apply)
    {
        if (minArgs < 0 || maxArgs < minArgs) throw new ArgumentOutOfRangeException(nameof(maxArgs));
        Name = name;
        MinArgs = minArgs;
        MaxArgs = maxArgs;
        Apply = apply;
    }

    public string Name { get; }
    public int MinArgs { get; }
    public int MaxArgs { get; }
    public Func<IReadOnlyList<BigInteger>, BigInteger> Apply { get; }
}

/// <summary>Set of available functions; new functions are registered without changing the parser or evaluator.</summary>
public sealed class FunctionRegistry
{
    private readonly Dictionary<string, FunctionDefinition> _functions = new(StringComparer.OrdinalIgnoreCase);

    // Built-in functions of the lab variants
    public static FunctionRegistry CreateDefault() => new FunctionRegistry()
        .Register(new FunctionDefinition("inc", 1, 1, a => a[0] + 1))
        .Register(new FunctionDefinition("dec", 1, 1, a => a[0] - 1))
        .Register(new FunctionDefinition("max", 2, 2, a => BigInteger.Max(a[0], a[1])))
        .Register(new FunctionDefinition("min", 2, 2, a => BigInteger.Min(a[0], a[1])))
        .Register(new FunctionDefinition("mmax", 1, int.MaxValue, a => a.Aggregate(BigInteger.Max)))
        .Register(new FunctionDefinition("mmin", 1, int.MaxValue, a => a.Aggregate(BigInteger.Min)));

    public FunctionRegistry Register(FunctionDefinition function)
    {
        _functions[function.Name] = function;
        return this;
    }

    public bool TryGet(string name, out FunctionDefinition function) =>
        _functions.TryGetValue(name, out function!);
}
