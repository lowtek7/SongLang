using Song.Tokenizer;

namespace Song.Parser;

/// <summary>
/// Song 언어의 파서
/// 토큰 배열을 Statement 리스트로 변환한다.
/// </summary>
public sealed partial class Parser
{
    private readonly List<Token> _tokens;
    private int _current;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public List<Statement> Parse()
    {
        var statements = new List<Statement>();

        while (!IsAtEnd())
        {
            SkipNewlines();
            if (!IsAtEnd())
            {
                var stmt = ParseStatement();
                if (stmt is not null)
                {
                    statements.Add(stmt);
                }
            }
        }

        return statements;
    }

    #region Token Utilities

    private object? ParseSimpleValue()
    {
        Token token = Peek();

        return token.Type switch
        {
            TokenType.NUMBER => Advance().Value,
            TokenType.STRING => Advance().Value,
            TokenType.IDENTIFIER => Advance().Lexeme,
            _ => throw new ParserException($"Value (number, string, or identifier) expected. Found '{token.Lexeme}'", token)
        };
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.NEWLINE))
        {
            Advance();
        }
    }

    private bool CheckEndOfStatement()
    {
        return IsAtEnd() || Check(TokenType.NEWLINE) || Check(TokenType.END) || Check(TokenType.WHEN);
    }

    private bool CheckRelation()
    {
        TokenType type = Peek().Type;
        return type == TokenType.IS ||
               type == TokenType.HAS ||
               type == TokenType.DO ||
               type == TokenType.PRINT ||
               type == TokenType.CAN ||
               type == TokenType.LOSES ||
               type == TokenType.EACH ||
               type == TokenType.WHEN ||
               type == TokenType.CONTAINS ||
               type == TokenType.IN ||
               type == TokenType.CLEAR ||
               type == TokenType.IDENTIFIER;
    }

    private Token Advance()
    {
        if (!IsAtEnd())
        {
            _current++;
        }
        return Previous();
    }

    /// <summary>
    /// 기대하는 토큰 타입을 확인하고 소비한다.
    /// 일치하지 않으면 ParserException을 던진다.
    /// </summary>
    private Token Expect(TokenType expected, string what)
    {
        if (!Check(expected))
        {
            throw new ParserException($"{what} expected. Found '{Peek().Lexeme}'", Peek());
        }
        return Advance();
    }

    private Token Peek() => _tokens[_current];

    private Token Previous() => _tokens[_current - 1];

    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return Peek().Type == type;
    }

    private bool IsAtEnd() => Peek().Type == TokenType.EOF;

    #endregion
}
