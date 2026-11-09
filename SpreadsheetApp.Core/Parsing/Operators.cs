namespace SpreadsheetApp.Parsing;

public enum BinaryOp
{
    Add,            // +
    Subtract,       // -
    Multiply,       // *
    Divide,         // /
    IntDivide,      // div
    Modulo,         // mod
    Power,          // ^
    Equal,          // =
    NotEqual,       // <>
    Less,           // <
    LessOrEqual,    // <=
    Greater,        // >
    GreaterOrEqual, // >=
    And,            // and
    Or,             // or
    Eqv             // eqv
}

public enum UnaryOp
{
    Plus,
    Minus,
    Not
}
