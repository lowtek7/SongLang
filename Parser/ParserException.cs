using Song.Tokenizer;

namespace Song.Parser;

/// <summary>
/// 파서 오류
/// </summary>
public class ParserException : Exception
{
    public Token Token { get; }

    public ParserException(string message, Token token)
        : base($"[{token.Line}:{token.Column}] {message}")
    {
        Token = token;
    }
}
