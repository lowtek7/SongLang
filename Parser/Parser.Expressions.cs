using Song.Tokenizer;

namespace Song.Parser;

public sealed partial class Parser
{
    /// <summary>
    /// 괄호로 시작하는 표현식 문장 파싱: (Expression) PRINT
    /// </summary>
    private Statement ParseParenthesizedExpressionStatement()
    {
        var startToken = Peek();
        Advance(); // '('
        var expr = ParseExpression();
        Expect(TokenType.RPAREN, "')'");

        // 괄호로 감싸진 표현식 자체를 주어로 사용
        return ParseExpressionSubjectStatement(expr);
    }

    /// <summary>
    /// 표현식 주어 문장 파싱: Expression PRINT, Expression HAS Property Value
    /// </summary>
    private Statement ParseExpressionSubjectStatement(Expression subjectExpr)
    {
        if (!Check(TokenType.PRINT) && !Check(TokenType.HAS))
        {
            throw new ParserException($"PRINT or HAS expected after expression subject. Found '{Peek().Lexeme}'", Peek());
        }

        var relation = Advance();

        if (relation.Type == TokenType.PRINT)
        {
            return new ExpressionPrintStatement(subjectExpr, subjectExpr.Line, subjectExpr.Column);
        }

        // HAS
        var property = Expect(TokenType.IDENTIFIER, "Property name after HAS");

        // 괄호로 시작하면 표현식
        if (Check(TokenType.LPAREN))
        {
            Advance(); // '('
            var valueExpr = ParseExpression();
            Expect(TokenType.RPAREN, "')' after expression");
            return new ExpressionHasStatement(subjectExpr, property.Lexeme, valueExpr, subjectExpr.Line, subjectExpr.Column);
        }

        // 일반 값
        if (CheckEndOfStatement())
        {
            return new ExpressionHasStatement(subjectExpr, property.Lexeme, (object?)null, subjectExpr.Line, subjectExpr.Column);
        }

        var value = ParseSimpleValue();
        return new ExpressionHasStatement(subjectExpr, property.Lexeme, value, subjectExpr.Line, subjectExpr.Column);
    }

    #region Expression Parsing (Pratt Parser style)

    // 연산자 우선순위 (낮은 순):
    // 1. OR
    // 2. AND
    // 3. == != < > <= >=
    // 4. + -
    // 5. * / %
    // 6. NOT -
    // 7. . (속성 접근)
    // 8. () 괄호, 리터럴

    private Expression ParseExpression()
    {
        return ParseOr();
    }

    private Expression ParseOr()
    {
        var expr = ParseAnd();

        while (Check(TokenType.OR))
        {
            var opToken = Advance();
            var right = ParseAnd();
            expr = new BinaryExpression(expr, BinaryOperator.Or, right, expr.Line, expr.Column);
        }

        return expr;
    }

    private Expression ParseAnd()
    {
        var expr = ParseComparison();

        while (Check(TokenType.AND))
        {
            var opToken = Advance();
            var right = ParseComparison();
            expr = new BinaryExpression(expr, BinaryOperator.And, right, expr.Line, expr.Column);
        }

        return expr;
    }

    private Expression ParseComparison()
    {
        var expr = ParseAdditive();

        while (Check(TokenType.EQ) || Check(TokenType.NEQ) ||
               Check(TokenType.LT) || Check(TokenType.GT) ||
               Check(TokenType.LTE) || Check(TokenType.GTE))
        {
            var opToken = Advance();
            var op = GetBinaryOperator(opToken.Type);
            var right = ParseAdditive();
            expr = new BinaryExpression(expr, op, right, expr.Line, expr.Column);
        }

        return expr;
    }

    private Expression ParseAdditive()
    {
        var expr = ParseMultiplicative();

        while (Check(TokenType.PLUS) || Check(TokenType.MINUS))
        {
            var opToken = Advance();
            var op = GetBinaryOperator(opToken.Type);
            var right = ParseMultiplicative();
            expr = new BinaryExpression(expr, op, right, expr.Line, expr.Column);
        }

        return expr;
    }

    private Expression ParseMultiplicative()
    {
        var expr = ParseUnary();

        while (Check(TokenType.STAR) || Check(TokenType.SLASH) || Check(TokenType.MODULO))
        {
            var opToken = Advance();
            var op = GetBinaryOperator(opToken.Type);
            var right = ParseUnary();
            expr = new BinaryExpression(expr, op, right, expr.Line, expr.Column);
        }

        return expr;
    }

    private Expression ParseUnary()
    {
        // NOT (논리 부정)
        if (Check(TokenType.NOT))
        {
            var opToken = Advance();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryOperator.Not, operand, opToken.Line, opToken.Column);
        }

        // - (숫자 부정)
        if (Check(TokenType.MINUS))
        {
            var opToken = Advance();
            var operand = ParseUnary();
            return new UnaryExpression(UnaryOperator.Negate, operand, opToken.Line, opToken.Column);
        }

        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Check(TokenType.DOT))
            {
                // 점 표기법: Player.HP
                Advance(); // '.'
                if (!Check(TokenType.IDENTIFIER))
                {
                    throw new ParserException($"Property name expected after '.'. Found '{Peek().Lexeme}'", Peek());
                }
                var property = Advance();
                expr = new PropertyAccessExpression(expr, property.Lexeme, expr.Line, expr.Column);
            }
            else if (Check(TokenType.OF))
            {
                // OF 표기법: HP OF Player
                // expr이 식별자(속성 이름)여야 함
                if (expr is not IdentifierExpression propId)
                {
                    throw new ParserException("Property name must precede OF", Peek());
                }

                Advance(); // 'OF'

                // 객체 표현식 파싱 (재귀적으로 postfix 처리)
                var objExpr = ParsePostfix();

                // PropertyAccessExpression 생성 (HP OF Player -> Player.HP)
                expr = new PropertyAccessExpression(objExpr, propId.Name, expr.Line, expr.Column);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expression ParsePrimary()
    {
        var token = Peek();

        if (Check(TokenType.NUMBER))
        {
            Advance();
            return new NumberExpression((double)token.Value!, token.Line, token.Column, token.Lexeme.Contains('.'));
        }

        if (Check(TokenType.STRING))
        {
            Advance();
            return new StringExpression((string)token.Value!, token.Line, token.Column);
        }

        if (Check(TokenType.IDENTIFIER))
        {
            Advance();
            return new IdentifierExpression(token.Lexeme, token.Line, token.Column);
        }

        // 쿼리 변수 (?name) - 식별자처럼 취급
        if (Check(TokenType.QUERY_VAR))
        {
            Advance();
            var varName = (string)token.Value!;
            return new IdentifierExpression(varName, token.Line, token.Column);
        }

        if (Check(TokenType.LPAREN))
        {
            Advance(); // '('

            // 관계 호출 표현식 체크: (Subject Relation [Args...])
            // 첫 번째 토큰이 IDENTIFIER이고 두 번째도 IDENTIFIER이면 관계 호출
            if (Check(TokenType.IDENTIFIER))
            {
                int savedPos = _current;
                var firstIdent = Advance();

                if (Check(TokenType.IDENTIFIER))
                {
                    // 관계 호출 패턴: Subject Relation Args...
                    var relation = Advance();
                    var arguments = new List<string>();

                    while (Check(TokenType.IDENTIFIER))
                    {
                        arguments.Add(Advance().Lexeme);
                    }

                    Expect(TokenType.RPAREN, "')'");
                    return new RelationCallExpression(firstIdent.Lexeme, relation.Lexeme, arguments, token.Line, token.Column);
                }
                else
                {
                    // 일반 괄호 표현식 - 위치 복원
                    _current = savedPos;
                }
            }

            // 일반 괄호 표현식
            var inner = ParseExpression();
            Expect(TokenType.RPAREN, "')'");
            return new GroupingExpression(inner, token.Line, token.Column);
        }

        // RANDOM min max
        if (Check(TokenType.RANDOM))
        {
            Advance(); // RANDOM

            // min 파싱
            Expression minExpr;
            if (Check(TokenType.NUMBER))
            {
                var numToken = Advance();
                minExpr = new NumberExpression((double)numToken.Value!, numToken.Line, numToken.Column, numToken.Lexeme.Contains('.'));
            }
            else if (Check(TokenType.LPAREN))
            {
                Advance(); // '('
                minExpr = ParseExpression();
                Expect(TokenType.RPAREN, "')'");
            }
            else if (Check(TokenType.IDENTIFIER))
            {
                var idToken = Advance();
                minExpr = new IdentifierExpression(idToken.Lexeme, idToken.Line, idToken.Column);
                // 속성 접근 체인 처리
                while (Check(TokenType.DOT))
                {
                    Advance(); // '.'
                    var prop = Expect(TokenType.IDENTIFIER, "Property name after '.'");
                    minExpr = new PropertyAccessExpression(minExpr, prop.Lexeme, minExpr.Line, minExpr.Column);
                }
            }
            else
            {
                throw new ParserException($"Minimum value expected after RANDOM. Found '{Peek().Lexeme}'", Peek());
            }

            // max 파싱
            Expression maxExpr;
            if (Check(TokenType.NUMBER))
            {
                var numToken = Advance();
                maxExpr = new NumberExpression((double)numToken.Value!, numToken.Line, numToken.Column, numToken.Lexeme.Contains('.'));
            }
            else if (Check(TokenType.LPAREN))
            {
                Advance(); // '('
                maxExpr = ParseExpression();
                Expect(TokenType.RPAREN, "')'");
            }
            else if (Check(TokenType.IDENTIFIER))
            {
                var idToken = Advance();
                maxExpr = new IdentifierExpression(idToken.Lexeme, idToken.Line, idToken.Column);
                // 속성 접근 체인 처리
                while (Check(TokenType.DOT))
                {
                    Advance(); // '.'
                    var prop = Expect(TokenType.IDENTIFIER, "Property name after '.'");
                    maxExpr = new PropertyAccessExpression(maxExpr, prop.Lexeme, maxExpr.Line, maxExpr.Column);
                }
            }
            else
            {
                throw new ParserException($"Maximum value expected after RANDOM. Found '{Peek().Lexeme}'", Peek());
            }

            return new RandomExpression(minExpr, maxExpr, token.Line, token.Column);
        }

        throw new ParserException($"Expression expected. Found '{token.Lexeme}'", token);
    }

    private static BinaryOperator GetBinaryOperator(TokenType type) => type switch
    {
        TokenType.PLUS => BinaryOperator.Add,
        TokenType.MINUS => BinaryOperator.Subtract,
        TokenType.STAR => BinaryOperator.Multiply,
        TokenType.SLASH => BinaryOperator.Divide,
        TokenType.MODULO => BinaryOperator.Modulo,
        TokenType.EQ => BinaryOperator.Equal,
        TokenType.NEQ => BinaryOperator.NotEqual,
        TokenType.LT => BinaryOperator.LessThan,
        TokenType.GT => BinaryOperator.GreaterThan,
        TokenType.LTE => BinaryOperator.LessEqual,
        TokenType.GTE => BinaryOperator.GreaterEqual,
        TokenType.AND => BinaryOperator.And,
        TokenType.OR => BinaryOperator.Or,
        _ => throw new ArgumentException($"Unknown operator: {type}")
    };

    #endregion
}
