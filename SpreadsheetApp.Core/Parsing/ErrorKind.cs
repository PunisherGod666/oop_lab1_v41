namespace SpreadsheetApp.Parsing;

/// <summary>Kinds of parse and evaluation errors.</summary>
public enum ErrorKind
{
    Syntax,
    TypeMismatch,
    DivisionByZero,
    CyclicReference,
    InvalidReference,
    InvalidArgument
}
