using Song.Tokenizer;

namespace Song.Parser;

public sealed partial class Parser
{
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

        // 관계 타입 (IS, HAS, CAN, IN, CONTAINS 또는 사용자 정의 관계)
        if (!Check(TokenType.IS) && !Check(TokenType.HAS) && !Check(TokenType.CAN) &&
            !Check(TokenType.IN) && !Check(TokenType.CONTAINS) && !Check(TokenType.IDENTIFIER))
        {
            throw new ParserException($"Relation expected in query. Found '{Peek().Lexeme}'", Peek());
        }

        Token relationToken = Advance();
        string relation = relationToken.Lexeme;
        bool isBuiltInRelation = relationToken.Type == TokenType.IS ||
                                  relationToken.Type == TokenType.HAS ||
                                  relationToken.Type == TokenType.CAN ||
                                  relationToken.Type == TokenType.IN ||
                                  relationToken.Type == TokenType.CONTAINS;

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
}
