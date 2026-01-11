using Song.Tokenizer;

namespace Song.Parser;

/// <summary>
/// Song 언어의 파서
/// 토큰 배열을 Statement 리스트로 변환한다.
/// </summary>
public sealed class Parser
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

    private Statement? ParseStatement()
    {
        // DEBUG 문장 처리
        if (Check(TokenType.DEBUG))
        {
            return ParseDebug();
        }

        // ALL 문장 처리
        if (Check(TokenType.ALL))
        {
            return ParseAll();
        }

        // 쿼리 문장 처리 (? 또는 ?name으로 시작)
        if (Check(TokenType.QUESTION) || Check(TokenType.QUERY_VAR))
        {
            return ParseQuery();
        }

        // 괄호로 시작하는 표현식 (예: (HP OF Player) PRINT)
        if (Check(TokenType.LPAREN))
        {
            return ParseParenthesizedExpressionStatement();
        }

        // CHANCE 문장 처리
        if (Check(TokenType.CHANCE))
        {
            return ParseChance();
        }

        // 문장은 항상 IDENTIFIER로 시작 (Subject)
        Token subjectToken = Expect(TokenType.IDENTIFIER, "Statement must start with an identifier");

        // 체인된 속성 접근 확인 (Player.HP.Current 형태)
        Expression? subjectExpr = null;
        if (Check(TokenType.DOT))
        {
            subjectExpr = new IdentifierExpression(subjectToken.Lexeme, subjectToken.Line, subjectToken.Column);
            while (Check(TokenType.DOT))
            {
                Advance(); // '.'
                var property = Expect(TokenType.IDENTIFIER, "Property name after '.'");
                subjectExpr = new PropertyAccessExpression(subjectExpr, property.Lexeme, subjectExpr.Line, subjectExpr.Column);
            }
        }

        // 표현식 주어인 경우 (속성 접근 체인이 있는 경우)
        if (subjectExpr is not null)
        {
            return ParseExpressionSubjectStatement(subjectExpr);
        }

        // 단순 식별자 주어 - 기존 로직
        // Relation: 키워드 또는 식별자
        if (!CheckRelation())
        {
            throw new ParserException($"Relation expected. Found '{Peek().Lexeme}'", Peek());
        }

        Token relation = Advance();

        Statement stmt = relation.Type switch
        {
            TokenType.DO => ParseDoBlock(subjectToken),
            TokenType.PRINT => new RelationStatement(subjectToken.Lexeme, relation.Lexeme, null, null, subjectToken.Line, subjectToken.Column),
            TokenType.CAN => ParseCan(subjectToken),
            TokenType.LOSES => ParseLoses(subjectToken),
            TokenType.HAS => ParseHas(subjectToken),
            TokenType.IS => ParseIs(subjectToken),
            TokenType.EACH => ParseEach(subjectToken),
            TokenType.WHEN => ParseWhenExpression(subjectToken),  // Subject WHEN (condition) DO ... END
            _ => ParseCustomRelation(subjectToken, relation)
        };

        // 기존 WHEN 조건 체크 (RelationStatement WHEN DO ... END 형식)
        // RelationStatement만 WHEN 조건으로 사용 가능
        if (Check(TokenType.WHEN) && stmt is RelationStatement relStmt)
        {
            return ParseWhen(relStmt);
        }

        return stmt;
    }

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

    private DebugStatement ParseDebug()
    {
        Token debugToken = Advance(); // DEBUG
        Token targetToken = Expect(TokenType.IDENTIFIER, "DEBUG target (GRAPH, etc.)");
        DebugTarget target = targetToken.Lexeme.ToUpperInvariant() switch
        {
            "GRAPH" => DebugTarget.Graph,
            "TOKENS" => DebugTarget.Tokens,
            "AST" => DebugTarget.Ast,
            _ => throw new ParserException($"Unknown DEBUG target: {targetToken.Lexeme}", targetToken)
        };

        return new DebugStatement(target, debugToken.Line, debugToken.Column);
    }

    private Statement ParseIs(Token subject)
    {
        if (!Check(TokenType.IDENTIFIER) && !Check(TokenType.RELATION))
        {
            throw new ParserException($"Type expected after IS. Found '{Peek().Lexeme}'", Peek());
        }

        Token obj = Advance();
        return new RelationStatement(subject.Lexeme, "IS", obj.Lexeme, null, subject.Line, subject.Column);
    }

    private Statement ParseHas(Token subject)
    {
        // 메타 속성 체크: HAS INVERSE, HAS DIRECTION
        if (Check(TokenType.INVERSE))
        {
            Advance(); // INVERSE
            var inverseName = Expect(TokenType.IDENTIFIER, "Inverse relation name after INVERSE");
            return new MetaPropertyStatement(subject.Lexeme, MetaPropertyType.Inverse,
                inverseName.Lexeme, subject.Line, subject.Column);
        }

        if (Check(TokenType.DIRECTION))
        {
            Advance(); // DIRECTION
            var direction = Expect(TokenType.IDENTIFIER, "Direction type after DIRECTION");
            return new MetaPropertyStatement(subject.Lexeme, MetaPropertyType.Direction,
                direction.Lexeme, subject.Line, subject.Column);
        }

        // 관계 쿼리 체크: Subject HAS ?
        if (Check(TokenType.QUESTION) || Check(TokenType.QUERY_VAR))
        {
            Advance(); // ? or ?name
            return new RelationQueryStatement(subject.Lexeme, "HAS", null, subject.Line, subject.Column);
        }

        Token property = Expect(TokenType.IDENTIFIER, "Property name after HAS");

        // 괄호로 시작하면 표현식 또는 역할 정의
        if (Check(TokenType.LPAREN))
        {
            Advance(); // '('

            // (Node) 패턴 확인 - 역할 정의
            if (Check(TokenType.IDENTIFIER) && Peek().Lexeme.Equals("Node", StringComparison.OrdinalIgnoreCase))
            {
                Advance(); // 'Node'
                Expect(TokenType.RPAREN, "')' in role definition");
                return new RoleDefinitionStatement(subject.Lexeme, property.Lexeme, subject.Line, subject.Column);
            }

            // 일반 표현식
            // 이미 '(' 다음으로 넘어갔으므로 현재 위치에서 표현식 파싱
            var expr = ParseExpression();
            Expect(TokenType.RPAREN, "')' after expression");

            return new HasExpressionStatement(subject.Lexeme, property.Lexeme, expr, subject.Line, subject.Column);
        }

        // 일반 값
        if (CheckEndOfStatement())
        {
            return new RelationStatement(subject.Lexeme, "HAS", property.Lexeme, null, subject.Line, subject.Column);
        }

        object? value = ParseSimpleValue();
        return new RelationStatement(subject.Lexeme, "HAS", property.Lexeme, value, subject.Line, subject.Column);
    }

    private CanStatement ParseCan(Token subject)
    {
        Token ability = Expect(TokenType.IDENTIFIER, "Ability name after CAN");
        return new CanStatement(subject.Lexeme, ability.Lexeme, subject.Line, subject.Column);
    }

    private LosesStatement ParseLoses(Token subject)
    {
        // LOSES IS Parent 형태 확인
        if (Check(TokenType.IS))
        {
            Advance(); // IS
            Token parent = Expect(TokenType.IDENTIFIER, "Parent node name after LOSES IS");
            return new LosesStatement(subject.Lexeme, parent.Lexeme, LosesType.Is, subject.Line, subject.Column);
        }

        // LOSES Target 형태 (능력/속성 자동 감지)
        Token target = Expect(TokenType.IDENTIFIER, "Target after LOSES");
        return new LosesStatement(subject.Lexeme, target.Lexeme, LosesType.Auto, subject.Line, subject.Column);
    }

    private Statement ParseCustomRelation(Token subject, Token relation)
    {
        // 인자가 없는 경우
        if (CheckEndOfStatement())
        {
            return new RelationStatement(subject.Lexeme, relation.Lexeme, [], subject.Line, subject.Column);
        }

        // 관계 쿼리 체크: Subject RELATION ?
        if (Check(TokenType.QUESTION) || Check(TokenType.QUERY_VAR))
        {
            Advance(); // ? or ?name
            return new RelationQueryStatement(subject.Lexeme, relation.Lexeme, null, subject.Line, subject.Column);
        }

        // 모든 인자 수집 (문장 끝까지)
        var arguments = new List<object>();

        while (!CheckEndOfStatement())
        {
            if (Check(TokenType.IDENTIFIER))
            {
                arguments.Add(Advance().Lexeme);
            }
            else if (Check(TokenType.NUMBER))
            {
                arguments.Add(Advance().Value!);
            }
            else if (Check(TokenType.STRING))
            {
                arguments.Add(Advance().Value!);
            }
            else
            {
                throw new ParserException($"Argument (identifier, number, or string) expected. Found '{Peek().Lexeme}'", Peek());
            }
        }

        return new RelationStatement(subject.Lexeme, relation.Lexeme, arguments, subject.Line, subject.Column);
    }

    private DoBlockStatement ParseDoBlock(Token subject)
    {
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

        Expect(TokenType.END, "'END' to close DO block");

        return new DoBlockStatement(subject.Lexeme, body, subject.Line, subject.Column);
    }

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
            percent = new NumberExpression((double)numToken.Value!, numToken.Line, numToken.Column);
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
    /// ALL 문장 파싱: ALL TypeName [Action] 또는 ALL ?queryVar [Action]
    /// </summary>
    private AllStatement ParseAll()
    {
        Token allToken = Advance(); // ALL

        string typeName;
        string? queryVariable = null;

        // 쿼리 변수 (?var) 또는 타입 이름
        if (Check(TokenType.QUERY_VAR))
        {
            Token queryToken = Advance();
            queryVariable = (string)queryToken.Value!;
            typeName = queryVariable;  // 쿼리 변수명을 타입 이름으로도 사용
        }
        else if (Check(TokenType.IDENTIFIER))
        {
            Token typeToken = Advance();
            typeName = typeToken.Lexeme;
        }
        else
        {
            throw new ParserException($"Type name or query variable expected after ALL. Found '{Peek().Lexeme}'", Peek());
        }

        // 뒤에 액션이 있는지 확인
        if (CheckEndOfStatement())
        {
            return new AllStatement(typeName, queryVariable, null, allToken.Line, allToken.Column);
        }

        // 액션 파싱 (Relation 부분)
        if (!CheckRelation())
        {
            throw new ParserException($"Relation expected. Found '{Peek().Lexeme}'", Peek());
        }

        Token relation = Advance();

        // 임시 토큰 생성 (typeName용)
        Token typeToken2 = new(TokenType.IDENTIFIER, typeName, null, allToken.Line, allToken.Column);

        Statement action = relation.Type switch
        {
            TokenType.HAS => ParseHasForAll(typeToken2),
            TokenType.PRINT => new RelationStatement(typeName, relation.Lexeme, null, null, allToken.Line, allToken.Column),
            _ => ParseCustomRelationForAll(typeToken2, relation)
        };

        return new AllStatement(typeName, queryVariable, action, allToken.Line, allToken.Column);
    }

    private Statement ParseHasForAll(Token typeName)
    {
        Token property = Expect(TokenType.IDENTIFIER, "Property name after HAS");

        if (CheckEndOfStatement())
        {
            return new RelationStatement(typeName.Lexeme, "HAS", property.Lexeme, null, typeName.Line, typeName.Column);
        }

        object? value = ParseSimpleValue();
        return new RelationStatement(typeName.Lexeme, "HAS", property.Lexeme, value, typeName.Line, typeName.Column);
    }

    private Statement ParseCustomRelationForAll(Token typeName, Token relation)
    {
        if (CheckEndOfStatement())
        {
            return new RelationStatement(typeName.Lexeme, relation.Lexeme, null, null, typeName.Line, typeName.Column);
        }

        if (!Check(TokenType.IDENTIFIER) && !Check(TokenType.NUMBER) && !Check(TokenType.STRING))
        {
            throw new ParserException($"Object expected. Found '{Peek().Lexeme}'", Peek());
        }

        Token obj = Advance();

        if (CheckEndOfStatement())
        {
            return new RelationStatement(typeName.Lexeme, relation.Lexeme, obj.Lexeme, null, typeName.Line, typeName.Column);
        }

        object? value = ParseSimpleValue();
        return new RelationStatement(typeName.Lexeme, relation.Lexeme, obj.Lexeme, value, typeName.Line, typeName.Column);
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

    /// <summary>
    /// 쿼리 문장 파싱: ?var Relation Target [WHERE condition]
    /// 예: ?enemy IS Enemy
    /// 예: ?item HAS Enchanted
    /// 예: ?x IS Monster WHERE ?x.HP > 50
    /// 예: ? OWNS Sword (사용자 정의 관계 쿼리)
    /// </summary>
    private Statement ParseQuery()
    {
        Token queryToken = Advance(); // ? or ?name

        // 쿼리 패턴 생성
        QueryPattern pattern = queryToken.Type == TokenType.QUESTION
            ? QueryPattern.Wildcard()
            : QueryPattern.Variable((string)queryToken.Value!);

        // 관계 타입 (IS, HAS, CAN, 또는 사용자 정의 관계)
        if (!Check(TokenType.IS) && !Check(TokenType.HAS) && !Check(TokenType.CAN) && !Check(TokenType.IDENTIFIER))
        {
            throw new ParserException($"Relation expected in query. Found '{Peek().Lexeme}'", Peek());
        }

        Token relationToken = Advance();
        string relation = relationToken.Lexeme;
        bool isBuiltInRelation = relationToken.Type == TokenType.IS ||
                                  relationToken.Type == TokenType.HAS ||
                                  relationToken.Type == TokenType.CAN;

        // 사용자 정의 관계 쿼리: ? OWNS Sword 또는 ? OWNS ?
        if (!isBuiltInRelation)
        {
            string? targetName = null;
            if (Check(TokenType.IDENTIFIER))
            {
                targetName = Advance().Lexeme;
            }
            else if (Check(TokenType.QUESTION) || Check(TokenType.QUERY_VAR))
            {
                Advance(); // ? 또는 ?name 소비 (와일드카드)
                targetName = null;
            }
            return new RelationQueryStatement(null, relation, targetName, queryToken.Line, queryToken.Column);
        }

        string relationUpper = relation.ToUpperInvariant();

        // 대상 (타입명 또는 속성명)
        string? target = null;
        object? targetValue = null;

        if (Check(TokenType.IDENTIFIER))
        {
            target = Advance().Lexeme;

            // HAS의 경우 값도 있을 수 있음: ?x HAS HP 100
            if (relationUpper == "HAS" && !CheckEndOfQueryStatement())
            {
                if (Check(TokenType.NUMBER) || Check(TokenType.STRING) || Check(TokenType.IDENTIFIER))
                {
                    targetValue = ParseSimpleValue();
                }
            }
        }

        // WHERE 조건 확인
        Expression? whereCondition = null;
        if (Check(TokenType.WHERE))
        {
            Advance(); // WHERE
            whereCondition = ParseQueryCondition();
        }

        return new QueryStatement(pattern, relationUpper, target, targetValue, whereCondition,
            queryToken.Line, queryToken.Column);
    }

    /// <summary>
    /// 쿼리 WHERE 조건 파싱
    /// ?x.HP > 50 같은 표현식 파싱
    /// </summary>
    private Expression ParseQueryCondition()
    {
        return ParseComparison();
    }

    /// <summary>
    /// 쿼리 문장 끝 확인 (WHERE도 고려)
    /// </summary>
    private bool CheckEndOfQueryStatement()
    {
        return IsAtEnd() || Check(TokenType.NEWLINE) || Check(TokenType.END) || Check(TokenType.WHERE);
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
            return new NumberExpression((double)token.Value!, token.Line, token.Column);
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
                minExpr = new NumberExpression((double)numToken.Value!, numToken.Line, numToken.Column);
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
                maxExpr = new NumberExpression((double)numToken.Value!, numToken.Line, numToken.Column);
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
}

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
