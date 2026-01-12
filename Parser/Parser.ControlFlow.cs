using Song.Tokenizer;

namespace Song.Parser;

public sealed partial class Parser
{
    /// <summary>
    /// WHEN 조건문 파싱: Statement WHEN DO ... END
    /// </summary>
    private WhenStatement ParseWhen(RelationStatement condition)
    {
        Token whenToken = Advance(); // WHEN
        Expect(TokenType.DO, "'DO' after WHEN");
        SkipNewlines();

        var body = new List<Statement>();

        while (!Check(TokenType.END) && !IsAtEnd())
        {
            var stmt = ParseStatement();
            if (stmt is not null)
            {
                body.Add(stmt);
            }
            SkipNewlines();
        }

        Expect(TokenType.END, "'END' to close WHEN block");

        return new WhenStatement(condition, body, whenToken.Line, whenToken.Column);
    }

    /// <summary>
    /// WHEN 표현식 문장 파싱: Subject WHEN (condition) DO ... [ELSE ...] END
    /// 예: Player WHEN (HP < 50) DO ... END
    /// 예: Player WHEN (HP > 70) DO ... ELSE DO ... END
    /// 예: Player WHEN (HP > 70) DO ... ELSE WHEN (HP > 30) DO ... ELSE DO ... END
    /// </summary>
    private WhenExpressionStatement ParseWhenExpression(Token subject)
    {
        // (condition) 파싱
        Expect(TokenType.LPAREN, "'(' after WHEN");
        var condition = ParseExpression();
        Expect(TokenType.RPAREN, "')' after condition");

        // DO 블록
        Expect(TokenType.DO, "'DO' after WHEN condition");
        SkipNewlines();

        var body = new List<Statement>();

        // ELSE 또는 END까지 파싱
        while (!Check(TokenType.END) && !Check(TokenType.ELSE) && !IsAtEnd())
        {
            var stmt = ParseStatement();
            if (stmt is not null)
            {
                body.Add(stmt);
            }
            SkipNewlines();
        }

        List<Statement>? elseBody = null;
        WhenExpressionStatement? elseWhen = null;

        // ELSE 처리
        if (Check(TokenType.ELSE))
        {
            Advance(); // ELSE

            // ELSE WHEN (체이닝)
            if (Check(TokenType.WHEN))
            {
                Advance(); // WHEN
                // 같은 subject로 재귀 호출
                var elseWhenToken = new Token(TokenType.IDENTIFIER, subject.Lexeme, null, subject.Line, subject.Column);
                elseWhen = ParseWhenExpression(elseWhenToken);
                // elseWhen이 END까지 처리했으므로 바로 반환
                return new WhenExpressionStatement(subject.Lexeme, condition, body, null, elseWhen, subject.Line, subject.Column);
            }

            // ELSE DO ... END
            Expect(TokenType.DO, "'DO' or 'WHEN' after ELSE");
            SkipNewlines();

            elseBody = [];

            while (!Check(TokenType.END) && !IsAtEnd())
            {
                var stmt = ParseStatement();
                if (stmt is not null)
                {
                    elseBody.Add(stmt);
                }
                SkipNewlines();
            }
        }

        Expect(TokenType.END, "'END' to close WHEN block");

        return new WhenExpressionStatement(subject.Lexeme, condition, body, elseBody, null, subject.Line, subject.Column);
    }

    /// <summary>
    /// CHANCE 문장 파싱: CHANCE percent DO ... [ELSE DO ...] END
    /// 예: CHANCE 30 DO ... END
    /// 예: CHANCE 50 DO ... ELSE DO ... END
    /// </summary>
    private ChanceStatement ParseChance()
    {
        Token chanceToken = Advance(); // CHANCE

        // 확률 표현식 파싱
        Expression percent;
        if (Check(TokenType.NUMBER))
        {
            var numToken = Advance();
            percent = new NumberExpression((double)numToken.Value!, numToken.Line, numToken.Column, numToken.Lexeme.Contains('.'));
        }
        else if (Check(TokenType.LPAREN))
        {
            Advance(); // '('
            percent = ParseExpression();
            Expect(TokenType.RPAREN, "')'");
        }
        else
        {
            throw new ParserException($"Probability value expected after CHANCE. Found '{Peek().Lexeme}'", Peek());
        }

        // DO 블록
        Expect(TokenType.DO, "'DO' after CHANCE probability");
        SkipNewlines();

        var body = new List<Statement>();

        // ELSE 또는 END까지 파싱
        while (!Check(TokenType.END) && !Check(TokenType.ELSE) && !IsAtEnd())
        {
            var stmt = ParseStatement();
            if (stmt is not null)
            {
                body.Add(stmt);
            }
            SkipNewlines();
        }

        List<Statement>? elseBody = null;

        // ELSE 처리
        if (Check(TokenType.ELSE))
        {
            Advance(); // ELSE
            Expect(TokenType.DO, "'DO' after ELSE");
            SkipNewlines();

            elseBody = [];

            while (!Check(TokenType.END) && !IsAtEnd())
            {
                var stmt = ParseStatement();
                if (stmt is not null)
                {
                    elseBody.Add(stmt);
                }
                SkipNewlines();
            }
        }

        Expect(TokenType.END, "'END' to close CHANCE block");

        return new ChanceStatement(percent, body, elseBody, chanceToken.Line, chanceToken.Column);
    }

    /// <summary>
    /// EACH 문장 파싱: Subject EACH Variable DO ... END
    /// </summary>
    private EachStatement ParseEach(Token subject)
    {
        Token variable = Expect(TokenType.IDENTIFIER, "Variable name after EACH");
        Expect(TokenType.DO, "'DO' after EACH variable");
        SkipNewlines();

        var body = new List<Statement>();

        while (!Check(TokenType.END) && !IsAtEnd())
        {
            var stmt = ParseStatement();
            if (stmt is not null)
            {
                body.Add(stmt);
            }
            SkipNewlines();
        }

        Expect(TokenType.END, "'END' to close EACH block");

        return new EachStatement(subject.Lexeme, variable.Lexeme, body, subject.Line, subject.Column);
    }
}
