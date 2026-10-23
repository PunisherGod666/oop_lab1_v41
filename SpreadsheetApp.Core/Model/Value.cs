using System.Numerics;

namespace SpreadsheetApp.Model;

public enum ValueKind
{
    Number,
    Boolean
}

/// <summary>Cell value: an arbitrary-length integer or a boolean.</summary>
public readonly struct Value : IEquatable<Value>
{
    public const string TrueText = "TRUE";
    public const string FalseText = "FALSE";

    private Value(ValueKind kind, BigInteger number, bool boolean)
    {
        Kind = kind;
        Number = number;
        Boolean = boolean;
    }

    public ValueKind Kind { get; }
    public BigInteger Number { get; }
    public bool Boolean { get; }

    public static Value FromNumber(BigInteger number) => new(ValueKind.Number, number, false);

    public static Value FromBool(bool value) => new(ValueKind.Boolean, BigInteger.Zero, value);

    public override string ToString() =>
        Kind == ValueKind.Number ? Number.ToString() : (Boolean ? TrueText : FalseText);

    public bool Equals(Value other) =>
        Kind == other.Kind && (Kind == ValueKind.Number ? Number == other.Number : Boolean == other.Boolean);

    public override bool Equals(object? obj) => obj is Value other && Equals(other);

    public override int GetHashCode() =>
        Kind == ValueKind.Number ? HashCode.Combine(Kind, Number) : HashCode.Combine(Kind, Boolean);
}
