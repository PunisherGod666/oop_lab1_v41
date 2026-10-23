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

public enum FunctionKind
{
    Inc,   // inc(x)
    Dec,   // dec(x)
    Max,   // max(x, y)
    Min,   // min(x, y)
    MMax,  // mmax(x1, ..., xN), N >= 1
    MMin   // mmin(x1, ..., xN), N >= 1
}
