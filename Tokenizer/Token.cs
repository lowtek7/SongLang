namespace Song.Tokenizer;

/// <summary>
/// Song 언어의 토큰
/// </summary>
public sealed class Token
{
    public TokenType Type { get; }
    public string Lexeme { get; }
    public object? Value { get; }
    public int Line { get; }
    public int Column { get; }

    public Token(TokenType type, string lexeme, object? value, int line, int column)
    {
        Type = type;
        Lexeme = lexeme;
        Value = value;
        Line = line;
        Column = column;
    }

    public override string ToString()
    {
        return Value is null
            ? $"[{Type}] '{Lexeme}' at {Line}:{Column}"
            : $"[{Type}] '{Lexeme}' = {Value} at {Line}:{Column}";
    }
}
