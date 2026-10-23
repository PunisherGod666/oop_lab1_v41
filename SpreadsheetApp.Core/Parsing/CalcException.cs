namespace SpreadsheetApp.Parsing;

/// <summary>Parse or evaluation error with its position in the expression (-1 if unknown).</summary>
public class CalcException : Exception
{
    public CalcException(ErrorKind kind, string message, int position = -1)
        : base(message)
    {
        Kind = kind;
        Position = position;
    }

    public ErrorKind Kind { get; }
    public int Position { get; }

    // Short code shown in the cell, Excel style
    public string Code => Kind switch
    {
        ErrorKind.Syntax => "#SYNTAX!",
        ErrorKind.TypeMismatch => "#VALUE!",
        ErrorKind.DivisionByZero => "#DIV/0!",
        ErrorKind.CyclicReference => "#CYCLE!",
        ErrorKind.InvalidReference => "#REF!",
        ErrorKind.InvalidArgument => "#NUM!",
        _ => "#ERROR!"
    };

    // Message with a 1-based position for the user
    public string DetailedMessage =>
        Position >= 0 ? $"{Message} (позиція {Position + 1})" : Message;
}
