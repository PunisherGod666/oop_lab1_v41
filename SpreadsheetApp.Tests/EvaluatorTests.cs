using System.Numerics;
using SpreadsheetApp.Model;
using SpreadsheetApp.Parsing;

namespace SpreadsheetApp.Tests;

public class EvaluatorTests
{
    private static Value Eval(string text, Func<CellAddress, Value>? cells = null)
    {
        var ast = FormulaParser.Parse(text);
        return new Evaluator(cells ?? (_ => Value.FromNumber(0))).Evaluate(ast);
    }

    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("10 - 4 - 3", "3")]
    [InlineData("7 / 2", "3")]
    [InlineData("-7 div 2", "-3")]
    [InlineData("7 mod 3", "1")]
    [InlineData("-5", "-5")]
    [InlineData("+5", "5")]
    [InlineData("--5", "5")]
    [InlineData("2 ^ 10", "1024")]
    [InlineData("2 ^ 3 ^ 2", "512")]
    [InlineData("-2 ^ 2", "-4")]
    [InlineData("inc(5)", "6")]
    [InlineData("dec(5)", "4")]
    [InlineData("max(3, 9)", "9")]
    [InlineData("min(3, 9)", "3")]
    [InlineData("mmax(4, 17, -2, 8)", "17")]
    [InlineData("mmin(4, 17, -2, 8)", "-2")]
    [InlineData("mmax(5)", "5")]
    [InlineData("=1+1", "2")]
    public void Arithmetic(string expression, string expected)
    {
        Assert.Equal(expected, Eval(expression).ToString());
    }

    [Fact]
    public void ArbitraryLengthIntegers()
    {
        var value = Eval("123456789012345678901234567890 * 10 + 2 ^ 100");
        var expected = BigInteger.Parse("1234567890123456789012345678900") + BigInteger.Pow(2, 100);
        Assert.Equal(expected, value.Number);
    }

    [Theory]
    [InlineData("1 = 1", true)]
    [InlineData("1 <> 1", false)]
    [InlineData("1 < 2", true)]
    [InlineData("2 <= 2", true)]
    [InlineData("3 > 4", false)]
    [InlineData("4 >= 5", false)]
    [InlineData("not (1 = 2)", true)]
    [InlineData("1 < 2 and 3 < 4", true)]
    [InlineData("1 > 2 or 3 > 4", false)]
    [InlineData("(1 > 2) eqv (3 > 4)", true)]
    [InlineData("(1 < 2) eqv (3 > 4)", false)]
    [InlineData("not 1 = 2 and 2 = 2", true)]
    [InlineData("(1 < 2) = (3 < 4)", true)]
    public void Logical(string expression, bool expected)
    {
        var value = Eval(expression);
        Assert.Equal(ValueKind.Boolean, value.Kind);
        Assert.Equal(expected, value.Boolean);
    }

    [Fact]
    public void CellReferencesAreResolved()
    {
        var cells = new Dictionary<CellAddress, Value>
        {
            [CellAddress.Parse("A1")] = Value.FromNumber(10),
            [CellAddress.Parse("b2")] = Value.FromNumber(5),
        };
        Assert.Equal(new BigInteger(25), Eval("A1 + b2 * 3", a => cells[a]).Number);
    }

    [Theory]
    [InlineData("1 / 0", ErrorKind.DivisionByZero)]
    [InlineData("1 mod 0", ErrorKind.DivisionByZero)]
    [InlineData("1 and 2", ErrorKind.TypeMismatch)]
    [InlineData("(1 < 2) + 1", ErrorKind.TypeMismatch)]
    [InlineData("1 = (1 < 2)", ErrorKind.TypeMismatch)]
    [InlineData("2 ^ -1", ErrorKind.InvalidArgument)]
    public void EvaluationErrors(string expression, ErrorKind kind)
    {
        var ex = Assert.Throws<CalcException>(() => Eval(expression));
        Assert.Equal(kind, ex.Kind);
    }

    [Theory]
    [InlineData("1 +", 3)]
    [InlineData("(1 + 2", 6)]
    [InlineData("1 + 2)", 5)]
    [InlineData("1 # 2", 2)]
    [InlineData("foo(1)", 0)]
    [InlineData("max(1)", 0)]
    [InlineData("mmax()", 0)]
    [InlineData("", 0)]
    [InlineData("1A", 0)]
    [InlineData("A1B + 1", 0)]
    [InlineData("A1 B1", 3)]
    [InlineData("max(1, 2", 8)]
    [InlineData("1 2", 2)]
    public void SyntaxErrorsAreLocalized(string expression, int position)
    {
        var ex = Assert.Throws<CalcException>(() => Eval(expression));
        Assert.Equal(ErrorKind.Syntax, ex.Kind);
        Assert.Equal(position, ex.Position);
    }

    [Theory]
    [InlineData("(1 + 2", "Очікувалося «)»")]
    [InlineData("1 +", "Неочікуваний кінець виразу")]
    [InlineData("1 # 2", "Невідомий символ «#»")]
    [InlineData("1A", "Некоректне число або посилання")]
    [InlineData("1 2", "Неочікувана лексема «2»")]
    [InlineData("foo(1)", "Невідома функція «foo»")]
    public void SyntaxErrorMessagesAreUkrainian(string expression, string expectedStart)
    {
        var ex = Assert.Throws<CalcException>(() => Eval(expression));
        Assert.StartsWith(expectedStart, ex.Message);
    }

    [Theory]
    [InlineData("1 / 0", "#DIV/0!")]
    [InlineData("1 and 2", "#VALUE!")]
    [InlineData("2 ^ -1", "#NUM!")]
    [InlineData("1 +", "#SYNTAX!")]
    public void ErrorCodesAreInEnglish(string expression, string code)
    {
        var ex = Assert.Throws<CalcException>(() => Eval(expression));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void KeywordsAndCellsAreCaseInsensitive()
    {
        var cells = new Dictionary<CellAddress, Value> { [CellAddress.Parse("A1")] = Value.FromNumber(3) };
        Assert.Equal("TRUE", Eval("NOT (a1 = 4) AND Mmax(a1, 2) = 3", a => cells[a]).ToString());
    }
}
