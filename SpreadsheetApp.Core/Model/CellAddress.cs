using System.Diagnostics.CodeAnalysis;

namespace SpreadsheetApp.Model;

/// <summary>Zero-based cell position, written as A1-style text.</summary>
public readonly struct CellAddress : IEquatable<CellAddress>
{
    public CellAddress(int row, int column)
    {
        if (row < 0) throw new ArgumentOutOfRangeException(nameof(row));
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));
        Row = row;
        Column = column;
    }

    public int Row { get; }
    public int Column { get; }

    public static CellAddress Parse(string text)
    {
        if (!TryParse(text, out var address))
            throw new FormatException($"Некоректна адреса клітинки «{text}»");
        return address;
    }

    // Parses letters (column) followed by digits (1-based row), e.g. "AB12"
    public static bool TryParse(string? text, out CellAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        int i = 0;
        long column = 0;
        while (i < text.Length && char.IsAsciiLetter(text[i]))
        {
            column = column * 26 + (char.ToUpperInvariant(text[i]) - 'A' + 1);
            if (column > int.MaxValue) return false;
            i++;
        }
        if (i == 0 || i == text.Length)
            return false;

        long row = 0;
        for (; i < text.Length; i++)
        {
            if (!char.IsAsciiDigit(text[i])) return false;
            row = row * 10 + (text[i] - '0');
            if (row > int.MaxValue) return false;
        }
        if (row < 1)
            return false;

        address = new CellAddress((int)row - 1, (int)column - 1);
        return true;
    }

    // 0 -> A, 25 -> Z, 26 -> AA
    public static string ColumnName(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        var chars = new Stack<char>();
        int n = index + 1;
        while (n > 0)
        {
            n--;
            chars.Push((char)('A' + n % 26));
            n /= 26;
        }
        return new string(chars.ToArray());
    }

    public override string ToString() => $"{ColumnName(Column)}{Row + 1}";

    public bool Equals(CellAddress other) => Row == other.Row && Column == other.Column;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CellAddress other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Row, Column);

    public static bool operator ==(CellAddress left, CellAddress right) => left.Equals(right);
    public static bool operator !=(CellAddress left, CellAddress right) => !left.Equals(right);
}
