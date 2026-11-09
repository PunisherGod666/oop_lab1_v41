using Antlr4.Runtime;
using SpreadsheetApp.Parsing.Grammar;

namespace SpreadsheetApp.Parsing;

/// <summary>Turns cell text into an expression tree.</summary>
public interface IFormulaParser
{
    Expr Parse(string text);
}

/// <summary>Parses cell text with the ANTLR-generated lexer/parser and builds an Expr tree.</summary>
public sealed class FormulaParser : IFormulaParser
{
    private readonly FunctionRegistry _functions;

    public FormulaParser(FunctionRegistry? functions = null)
    {
        _functions = functions ?? FunctionRegistry.CreateDefault();
    }

    public Expr Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new CalcException(ErrorKind.Syntax, "Порожній вираз", 0);

        var lexer = new CellFormulaLexer(new AntlrInputStream(text));
        lexer.RemoveErrorListeners();

        var parser = new CellFormulaParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(FormulaErrorListener.Instance);

        CellFormulaParser.FormulaContext tree = parser.formula();
        return new AstBuilder(_functions).Visit(tree);
    }
}

/// <summary>Turns the first ANTLR syntax error into a CalcException with a readable message.</summary>
internal sealed class FormulaErrorListener : BaseErrorListener
{
    public static readonly FormulaErrorListener Instance = new();

    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        bool expectsClosingParen = recognizer is Antlr4.Runtime.Parser parser &&
                                   parser.GetExpectedTokens().Contains(CellFormulaLexer.RPAREN);

        string message = offendingSymbol.Type switch
        {
            TokenConstants.EOF when expectsClosingParen => "Очікувалося «)», а знайдено кінець виразу",
            TokenConstants.EOF => "Неочікуваний кінець виразу",
            CellFormulaLexer.UNKNOWN => $"Невідомий символ «{offendingSymbol.Text}»",
            CellFormulaLexer.BAD_WORD => $"Некоректне число або посилання на клітинку «{offendingSymbol.Text}»",
            _ => $"Неочікувана лексема «{offendingSymbol.Text}»"
        };

        throw new CalcException(ErrorKind.Syntax, message, offendingSymbol.StartIndex);
    }
}
