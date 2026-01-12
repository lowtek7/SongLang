using Song.Tokenizer;

namespace Song.Parser;

public sealed partial class Parser
{
    private Statement? ParseStatement()
    {
        // GIVES 문장 처리 (DO 블록 내에서 값 반환)
        if (Check(TokenType.GIVES))
        {
            return ParseGives();
        }

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
            TokenType.CONTAINS => ParseContains(subjectToken),
            TokenType.IN => ParseIn(subjectToken),
            TokenType.EACH => ParseEach(subjectToken),
            TokenType.WHEN => ParseWhenExpression(subjectToken),  // Subject WHEN (condition) DO ... END
            TokenType.CLEAR => new RelationStatement(subjectToken.Lexeme, "CLEAR", null, null, subjectToken.Line, subjectToken.Column),
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

    /// <summary>
    /// GIVES 문장 파싱: GIVES Expression
    /// DO 블록 내에서 값을 반환한다.
    /// </summary>
    private GivesStatement ParseGives()
    {
        Token givesToken = Advance(); // GIVES

        // 표현식 파싱 (괄호 필수 아님)
        Expression value;
        if (Check(TokenType.LPAREN))
        {
            Advance(); // '('
            value = ParseExpression();
            Expect(TokenType.RPAREN, "')'");
        }
        else
        {
            value = ParseExpression();
        }

        return new GivesStatement(value, givesToken.Line, givesToken.Column);
    }

    private Statement ParseIs(Token subject)
    {
        if (!Check(TokenType.IDENTIFIER) && !Check(TokenType.RELATION))
        {
            throw new ParserException($"Type expected after IS. Found '{Peek().Lexeme}'", Peek());
        }

        Token obj = Advance();

        // RELATION 뒤에 (A, B, C) 형태의 역할 목록이 있는 경우
        if (obj.Lexeme.Equals("RELATION", StringComparison.OrdinalIgnoreCase) && Check(TokenType.LPAREN))
        {
            var roles = ParseRoleList();
            return new RelationDefinitionStatement(subject.Lexeme, roles, subject.Line, subject.Column);
        }

        return new RelationStatement(subject.Lexeme, "IS", obj.Lexeme, null, subject.Line, subject.Column);
    }

    /// <summary>
    /// CONTAINS 파싱: Subject CONTAINS Object
    /// 예: Inventory CONTAINS Sword
    /// </summary>
    private Statement ParseContains(Token subject)
    {
        Token obj = Expect(TokenType.IDENTIFIER, "Object name after CONTAINS");
        return new RelationStatement(subject.Lexeme, "CONTAINS", obj.Lexeme, null, subject.Line, subject.Column);
    }

    /// <summary>
    /// IN 파싱: Subject IN Container
    /// 예: Sword IN Inventory
    /// </summary>
    private Statement ParseIn(Token subject)
    {
        Token container = Expect(TokenType.IDENTIFIER, "Container name after IN");
        return new RelationStatement(subject.Lexeme, "IN", container.Lexeme, null, subject.Line, subject.Column);
    }

    private List<string> ParseRoleList()
    {
        Advance(); // '('
        var roles = new List<string>();

        // 첫 번째 역할
        var first = Expect(TokenType.IDENTIFIER, "Role name");
        roles.Add(first.Lexeme);

        // 콤마로 구분된 나머지 역할들
        while (Check(TokenType.COMMA))
        {
            Advance(); // ','
            var role = Expect(TokenType.IDENTIFIER, "Role name after ','");
            roles.Add(role.Lexeme);
        }

        Expect(TokenType.RPAREN, "')'");
        return roles;
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
            // (Node) 패턴 확인 - 역할 정의 (lookahead)
            int savedPos = _current;
            Advance(); // '('
            if (Check(TokenType.IDENTIFIER) && Peek().Lexeme.Equals("Node", StringComparison.OrdinalIgnoreCase))
            {
                int savedPos2 = _current;
                Advance(); // 'Node'
                if (Check(TokenType.RPAREN))
                {
                    Advance(); // ')'
                    return new RoleDefinitionStatement(subject.Lexeme, property.Lexeme, subject.Line, subject.Column);
                }
                // 아니면 복원
                _current = savedPos2;
            }
            // 일반 표현식 - 위치 복원하고 ParseExpression이 괄호 처리하도록
            _current = savedPos;
            var expr = ParseExpression();
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

        // LOSES CONTAINS Child 형태 확인
        if (Check(TokenType.CONTAINS))
        {
            Advance(); // CONTAINS
            Token child = Expect(TokenType.IDENTIFIER, "Child node name after LOSES CONTAINS");
            return new LosesStatement(subject.Lexeme, child.Lexeme, LosesType.Contains, subject.Line, subject.Column);
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
}
