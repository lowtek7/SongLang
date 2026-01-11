namespace Song.Tokenizer;

/// <summary>
/// Song 언어의 토크나이저
/// 소스 코드를 토큰 배열로 변환한다.
/// </summary>
public sealed class Tokenizer
{
    private readonly string _source;
    private readonly List<Token> _tokens = [];

    private int _start;
    private int _current;
    private int _line = 1;
    private int _column = 1;
    private int _tokenStartColumn = 1;

    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IS"] = TokenType.IS,
        ["HAS"] = TokenType.HAS,
        ["DO"] = TokenType.DO,
        ["END"] = TokenType.END,
        ["PRINT"] = TokenType.PRINT,
        ["CAN"] = TokenType.CAN,
        ["LOSES"] = TokenType.LOSES,
        ["RELATION"] = TokenType.RELATION,
        ["DEBUG"] = TokenType.DEBUG,
        ["WHEN"] = TokenType.WHEN,
        ["ELSE"] = TokenType.ELSE,
        ["ALL"] = TokenType.ALL,
        ["EACH"] = TokenType.EACH,
        ["WHERE"] = TokenType.WHERE,
        ["OF"] = TokenType.OF,
        ["RANDOM"] = TokenType.RANDOM,
        ["CHANCE"] = TokenType.CHANCE,
        ["INVERSE"] = TokenType.INVERSE,
        ["DIRECTION"] = TokenType.DIRECTION,
        ["AND"] = TokenType.AND,
        ["OR"] = TokenType.OR,
        ["NOT"] = TokenType.NOT
    };

    public Tokenizer(string source)
    {
        _source = source;
    }

    public List<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            _start = _current;
            _tokenStartColumn = _column;
            ScanToken();
        }

        _tokens.Add(new Token(TokenType.EOF, "", null, _line, _column));
        return _tokens;
    }

    private void ScanToken()
    {
        char c = Advance();

        switch (c)
        {
            case '{':
                AddToken(TokenType.LBRACE);
                break;
            case '}':
                AddToken(TokenType.RBRACE);
                break;
            case '(':
                AddToken(TokenType.LPAREN);
                break;
            case ')':
                AddToken(TokenType.RPAREN);
                break;
            case ',':
                AddToken(TokenType.COMMA);
                break;
            case '.':
                AddToken(TokenType.DOT);
                break;
            case '+':
                AddToken(TokenType.PLUS);
                break;
            case '-':
                AddToken(TokenType.MINUS);
                break;
            case '*':
                AddToken(TokenType.STAR);
                break;
            case '%':
                AddToken(TokenType.MODULO);
                break;
            case '/':
                if (Peek() == '/')
                {
                    // 주석: 줄 끝까지 무시
                    while (Peek() != '\n' && !IsAtEnd())
                    {
                        Advance();
                    }
                }
                else
                {
                    AddToken(TokenType.SLASH);
                }
                break;
            case '=':
                if (Peek() == '=')
                {
                    Advance();
                    AddToken(TokenType.EQ);
                }
                else
                {
                    throw new TokenizerException("'=' cannot be used alone, use '==' instead", _line, _tokenStartColumn);
                }
                break;
            case '!':
                if (Peek() == '=')
                {
                    Advance();
                    AddToken(TokenType.NEQ);
                }
                else
                {
                    throw new TokenizerException("'!' cannot be used alone, use '!=' instead", _line, _tokenStartColumn);
                }
                break;
            case '<':
                if (Peek() == '=')
                {
                    Advance();
                    AddToken(TokenType.LTE);
                }
                else
                {
                    AddToken(TokenType.LT);
                }
                break;
            case '>':
                if (Peek() == '=')
                {
                    Advance();
                    AddToken(TokenType.GTE);
                }
                else
                {
                    AddToken(TokenType.GT);
                }
                break;
            case '\n':
                AddToken(TokenType.NEWLINE);
                _line++;
                _column = 1;
                break;
            case '\r':
                // Windows CRLF: \r 무시, \n에서 처리
                break;
            case ' ':
            case '\t':
                // 공백 무시
                break;
            case '?':
                ScanQuery();
                break;
            case '"':
                ScanString();
                break;
            default:
                if (IsDigit(c))
                {
                    ScanNumber();
                }
                else if (IsAlpha(c))
                {
                    ScanIdentifier();
                }
                else
                {
                    throw new TokenizerException($"Unexpected character '{c}'", _line, _tokenStartColumn);
                }
                break;
        }
    }

    private void ScanString()
    {
        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\n')
            {
                _line++;
                _column = 0;
            }
            Advance();
        }

        if (IsAtEnd())
        {
            throw new TokenizerException("Unterminated string", _line, _tokenStartColumn);
        }

        // 닫는 따옴표
        Advance();

        // 따옴표 제거한 값
        string value = _source[(_start + 1)..(_current - 1)];
        AddToken(TokenType.STRING, value);
    }

    private void ScanNumber()
    {
        while (IsDigit(Peek()))
        {
            Advance();
        }

        // 소수점 처리
        if (Peek() == '.' && IsDigit(PeekNext()))
        {
            Advance(); // '.' 소비

            while (IsDigit(Peek()))
            {
                Advance();
            }
        }

        string lexeme = _source[_start.._current];
        double value = double.Parse(lexeme);
        AddToken(TokenType.NUMBER, value);
    }

    private void ScanIdentifier()
    {
        while (IsAlphaNumeric(Peek()))
        {
            Advance();
        }

        string text = _source[_start.._current];

        // 키워드 확인 (대소문자 무시)
        TokenType type = Keywords.TryGetValue(text, out TokenType keyword)
            ? keyword
            : TokenType.IDENTIFIER;

        AddToken(type);
    }

    private void ScanQuery()
    {
        // ? 다음에 식별자가 오면 QUERY_VAR (?name)
        // 아니면 QUESTION (?)
        if (IsAlpha(Peek()))
        {
            // ?name 패턴
            while (IsAlphaNumeric(Peek()))
            {
                Advance();
            }

            // ? 제외한 변수명
            string varName = _source[(_start + 1).._current];
            AddToken(TokenType.QUERY_VAR, varName);
        }
        else
        {
            // 단독 ?
            AddToken(TokenType.QUESTION);
        }
    }

    private char Advance()
    {
        _column++;
        return _source[_current++];
    }

    private char Peek()
    {
        if (IsAtEnd()) return '\0';
        return _source[_current];
    }

    private char PeekNext()
    {
        if (_current + 1 >= _source.Length) return '\0';
        return _source[_current + 1];
    }

    private bool IsAtEnd() => _current >= _source.Length;

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static bool IsAlpha(char c) =>
        (c >= 'a' && c <= 'z') ||
        (c >= 'A' && c <= 'Z') ||
        c == '_';

    private static bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);

    private void AddToken(TokenType type, object? value = null)
    {
        string lexeme = _source[_start.._current];
        _tokens.Add(new Token(type, lexeme, value, _line, _tokenStartColumn));
    }
}

/// <summary>
/// 토크나이저 오류
/// </summary>
public class TokenizerException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public TokenizerException(string message, int line, int column)
        : base($"[{line}:{column}] {message}")
    {
        Line = line;
        Column = column;
    }
}
