namespace Song.Parser;

/// <summary>
/// Song 언어의 문장
/// 형태: Subject Relation [Object] [Value]
/// </summary>
public abstract class Statement
{
    public int Line { get; }
    public int Column { get; }

    protected Statement(int line, int column)
    {
        Line = line;
        Column = column;
    }
}

/// <summary>
/// 관계 문장: Subject Relation [Args...]
/// 예: Player IS Entity
/// 예: Player HAS HP 100
/// 예: Message PRINT
/// 예: Player Attack Enemy (2개 인자)
/// 예: Player Trade Merchant Sword (3개 인자)
/// </summary>
public sealed class RelationStatement : Statement
{
    public string Subject { get; }
    public string Relation { get; }
    public List<object> Arguments { get; }

    // 기존 호환성을 위한 프로퍼티
    public string? Object => Arguments.Count > 0 ? Arguments[0]?.ToString() : null;
    public object? Value => Arguments.Count > 1 ? Arguments[1] : null;

    public RelationStatement(string subject, string relation, string? obj, object? value, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Relation = relation;
        Arguments = [];
        if (obj is not null) Arguments.Add(obj);
        if (value is not null) Arguments.Add(value);
    }

    public RelationStatement(string subject, string relation, List<object> arguments, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Relation = relation;
        Arguments = arguments;
    }

    public override string ToString()
    {
        if (Arguments.Count == 0)
            return $"{Subject} {Relation}";

        var argsStr = string.Join(" ", Arguments.Select(a => a is string s ? $"\"{s}\"" : a?.ToString() ?? "null"));
        return $"{Subject} {Relation} {argsStr}";
    }
}

/// <summary>
/// 표현식 기반 HAS 문장: Subject HAS Property (Expression)
/// 예: Target HAS HP (Target.HP - Damage)
/// </summary>
public sealed class HasExpressionStatement : Statement
{
    public string Subject { get; }
    public string Property { get; }
    public Expression ValueExpression { get; }

    public HasExpressionStatement(string subject, string property, Expression valueExpr, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Property = property;
        ValueExpression = valueExpr;
    }

    public override string ToString()
    {
        return $"{Subject} HAS {Property} ({ValueExpression})";
    }
}

/// <summary>
/// 표현식 주어 PRINT 문장: Expression PRINT
/// 예: Player.HP.Current PRINT
/// 예: (HP OF Player) PRINT
/// </summary>
public sealed class ExpressionPrintStatement : Statement
{
    public Expression Subject { get; }

    public ExpressionPrintStatement(Expression subject, int line, int column)
        : base(line, column)
    {
        Subject = subject;
    }

    public override string ToString() => $"{Subject} PRINT";
}

/// <summary>
/// 표현식 주어 HAS 문장: Expression HAS Property [Value|(Expression)]
/// 예: Player.HP HAS Current 100
/// 예: Player.HP HAS Current (Player.HP.Current - 10)
/// </summary>
public sealed class ExpressionHasStatement : Statement
{
    public Expression Subject { get; }
    public string Property { get; }
    public object? Value { get; }
    public Expression? ValueExpression { get; }

    public ExpressionHasStatement(Expression subject, string property, object? value, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Property = property;
        Value = value;
        ValueExpression = null;
    }

    public ExpressionHasStatement(Expression subject, string property, Expression valueExpr, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Property = property;
        Value = null;
        ValueExpression = valueExpr;
    }

    public override string ToString()
    {
        if (ValueExpression is not null)
            return $"{Subject} HAS {Property} ({ValueExpression})";
        return $"{Subject} HAS {Property} {Value}";
    }
}

/// <summary>
/// 역할 정의 문장: Subject HAS RoleName (Node)
/// 예: Attack HAS Attacker (Node)
/// 관계의 매개변수 역할을 정의한다.
/// </summary>
public sealed class RoleDefinitionStatement : Statement
{
    public string Subject { get; }
    public string RoleName { get; }

    public RoleDefinitionStatement(string subject, string roleName, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        RoleName = roleName;
    }

    public override string ToString() => $"{Subject} HAS {RoleName} (Node)";
}

/// <summary>
/// 메타 속성 타입
/// </summary>
public enum MetaPropertyType
{
    Inverse,    // HAS INVERSE
    Direction   // HAS DIRECTION
}

/// <summary>
/// 메타 속성 문장: Subject HAS INVERSE/DIRECTION Value
/// 예: OWNS HAS INVERSE OWNED_BY
/// 예: OWNS HAS DIRECTION BIDIRECTIONAL
/// 관계의 메타 속성을 정의한다.
/// </summary>
public sealed class MetaPropertyStatement : Statement
{
    public string Subject { get; }
    public MetaPropertyType Type { get; }
    public string Value { get; }

    public MetaPropertyStatement(string subject, MetaPropertyType type, string value, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Type = type;
        Value = value;
    }

    public override string ToString() => $"{Subject} HAS {Type.ToString().ToUpper()} {Value}";
}

/// <summary>
/// 관계 쿼리 문장: Subject RELATION ? 또는 ? RELATION Object
/// 예: Player OWNS ?          (Player가 OWNS하는 모든 것)
/// 예: ? OWNS Sword           (Sword를 OWNS하는 모든 것)
/// 예: Player HAS ?           (Player의 모든 관계)
/// </summary>
public sealed class RelationQueryStatement : Statement
{
    public string? Subject { get; }      // null이면 와일드카드 (?)
    public string RelationName { get; }
    public string? Object { get; }       // null이면 와일드카드 (?)

    public RelationQueryStatement(string? subject, string relationName, string? obj, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        RelationName = relationName;
        Object = obj;
    }

    public override string ToString()
    {
        var subj = Subject ?? "?";
        var obj = Object ?? "?";
        return $"{subj} {RelationName} {obj}";
    }
}

/// <summary>
/// DO 블록 문장: Subject DO ... END
/// 예: Attack DO ... END
/// </summary>
public sealed class DoBlockStatement : Statement
{
    public string Subject { get; }
    public List<Statement> Body { get; }

    public DoBlockStatement(string subject, List<Statement> body, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Body = body;
    }

    public override string ToString()
    {
        return $"{Subject} DO [{Body.Count} statements] END";
    }
}

/// <summary>
/// 능력 문장: Subject CAN Ability
/// 예: Player CAN ATTACK
/// </summary>
public sealed class CanStatement : Statement
{
    public string Subject { get; }
    public string Ability { get; }

    public CanStatement(string subject, string ability, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Ability = ability;
    }

    public override string ToString() => $"{Subject} CAN {Ability}";
}

/// <summary>
/// 관계 제거 문장: Subject LOSES [Type] Target
/// 예: Dragon LOSES FLY (능력 또는 속성)
/// 예: Player LOSES IS Entity (상속 관계)
/// </summary>
public sealed class LosesStatement : Statement
{
    public string Subject { get; }
    public string Target { get; }
    public LosesType Type { get; }

    public LosesStatement(string subject, string target, LosesType type, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Target = target;
        Type = type;
    }

    public override string ToString() => Type switch
    {
        LosesType.Is => $"{Subject} LOSES IS {Target}",
        _ => $"{Subject} LOSES {Target}"
    };
}

/// <summary>
/// LOSES 타입
/// </summary>
public enum LosesType
{
    Auto,   // 능력/속성 자동 감지
    Is,     // IS 관계 제거
}

/// <summary>
/// 디버그 문장: DEBUG Target
/// 예: DEBUG GRAPH
/// </summary>
public sealed class DebugStatement : Statement
{
    public DebugTarget Target { get; }

    public DebugStatement(DebugTarget target, int line, int column)
        : base(line, column)
    {
        Target = target;
    }

    public override string ToString() => $"DEBUG {Target}";
}

/// <summary>
/// 디버그 대상
/// </summary>
public enum DebugTarget
{
    Graph,      // 그래프 상태 출력
    Tokens,     // 토큰 출력 (향후)
    Ast         // AST 출력 (향후)
}

/// <summary>
/// WHEN 조건문: RelationStatement WHEN DO ... END
/// 예: Player HAS HP 0 WHEN DO ... END
/// 조건이 참일 때만 블록 실행 (HAS, IS, CAN 관계만 조건으로 사용 가능)
/// </summary>
public sealed class WhenStatement : Statement
{
    public RelationStatement Condition { get; }
    public List<Statement> Body { get; }

    public WhenStatement(RelationStatement condition, List<Statement> body, int line, int column)
        : base(line, column)
    {
        Condition = condition;
        Body = body;
    }

    public override string ToString()
    {
        return $"{Condition} WHEN DO [{Body.Count} statements] END";
    }
}

/// <summary>
/// WHEN 표현식 조건문: Subject WHEN (Expression) DO ... [ELSE DO ...] END
/// 예: Player WHEN (HP < 50) DO ... END
/// 예: Player WHEN (HP > 70) DO ... ELSE DO ... END
/// 예: Player WHEN (HP > 70) DO ... ELSE WHEN (HP > 30) DO ... ELSE DO ... END
/// </summary>
public sealed class WhenExpressionStatement : Statement
{
    public string Subject { get; }
    public Expression Condition { get; }
    public List<Statement> Body { get; }
    public List<Statement>? ElseBody { get; }           // ELSE DO ... END
    public WhenExpressionStatement? ElseWhen { get; }   // ELSE WHEN ... (체이닝)

    public WhenExpressionStatement(string subject, Expression condition, List<Statement> body, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Condition = condition;
        Body = body;
        ElseBody = null;
        ElseWhen = null;
    }

    public WhenExpressionStatement(string subject, Expression condition, List<Statement> body,
        List<Statement>? elseBody, WhenExpressionStatement? elseWhen, int line, int column)
        : base(line, column)
    {
        Subject = subject;
        Condition = condition;
        Body = body;
        ElseBody = elseBody;
        ElseWhen = elseWhen;
    }

    public override string ToString()
    {
        var result = $"{Subject} WHEN ({Condition}) DO [{Body.Count} statements]";
        if (ElseWhen is not null)
            result += $" ELSE {ElseWhen}";
        else if (ElseBody is not null)
            result += $" ELSE DO [{ElseBody.Count} statements]";
        return result + " END";
    }
}

/// <summary>
/// CHANCE 확률 분기문: CHANCE percent DO ... [ELSE DO ...] END
/// 예: CHANCE 30 DO ... END  // 30% 확률로 실행
/// 예: CHANCE 50 DO ... ELSE DO ... END  // 50% 확률, 아니면 ELSE
/// </summary>
public sealed class ChanceStatement : Statement
{
    public Expression Percent { get; }
    public List<Statement> Body { get; }
    public List<Statement>? ElseBody { get; }

    public ChanceStatement(Expression percent, List<Statement> body, int line, int column)
        : base(line, column)
    {
        Percent = percent;
        Body = body;
        ElseBody = null;
    }

    public ChanceStatement(Expression percent, List<Statement> body, List<Statement>? elseBody, int line, int column)
        : base(line, column)
    {
        Percent = percent;
        Body = body;
        ElseBody = elseBody;
    }

    public override string ToString()
    {
        var result = $"CHANCE {Percent} DO [{Body.Count} statements]";
        if (ElseBody is not null)
            result += $" ELSE DO [{ElseBody.Count} statements]";
        return result + " END";
    }
}

/// <summary>
/// ALL 쿼리문: ALL Subject Relation [Object] [Value]
/// 예: ALL Enemy HAS Stunned true
/// 예: ALL ?enemies PRINT  (쿼리 결과 사용)
/// 조건에 맞는 모든 노드에 적용
/// </summary>
public sealed class AllStatement : Statement
{
    public string TypeName { get; }
    public string? QueryVariable { get; }  // 쿼리 변수명 (?enemies -> "enemies")
    public Statement? Action { get; }

    public AllStatement(string typeName, Statement? action, int line, int column)
        : base(line, column)
    {
        TypeName = typeName;
        QueryVariable = null;
        Action = action;
    }

    public AllStatement(string typeName, string? queryVariable, Statement? action, int line, int column)
        : base(line, column)
    {
        TypeName = typeName;
        QueryVariable = queryVariable;
        Action = action;
    }

    public override string ToString()
    {
        var target = QueryVariable is not null ? $"?{QueryVariable}" : TypeName;
        return Action is null
            ? $"ALL {target}"
            : $"ALL {target} {Action}";
    }
}

/// <summary>
/// EACH 반복문: Subject EACH Variable DO ... END
/// 예: Inventory EACH Item DO ... END
/// 컬렉션의 각 요소에 대해 블록 실행
/// </summary>
public sealed class EachStatement : Statement
{
    public string Collection { get; }
    public string Variable { get; }
    public List<Statement> Body { get; }

    public EachStatement(string collection, string variable, List<Statement> body, int line, int column)
        : base(line, column)
    {
        Collection = collection;
        Variable = variable;
        Body = body;
    }

    public override string ToString()
    {
        return $"{Collection} EACH {Variable} DO [{Body.Count} statements] END";
    }
}

/// <summary>
/// 쿼리 패턴 요소
/// ? (와일드카드) 또는 ?name (변수 바인딩)
/// </summary>
public sealed class QueryPattern
{
    public bool IsWildcard { get; }       // true if ?, false if ?name
    public string? VariableName { get; }  // null if wildcard

    private QueryPattern(bool isWildcard, string? variableName)
    {
        IsWildcard = isWildcard;
        VariableName = variableName;
    }

    public static QueryPattern Wildcard() => new(true, null);
    public static QueryPattern Variable(string name) => new(false, name);

    public override string ToString() => IsWildcard ? "?" : $"?{VariableName}";
}

/// <summary>
/// 쿼리 문장: ?var Relation Target [WHERE condition]
/// 예: ?enemy IS Enemy                    // Enemy 타입 노드 검색
/// 예: ?item HAS Enchanted                // Enchanted 속성 있는 노드 검색
/// 예: ?x IS Monster WHERE ?x.HP > 50     // HP > 50인 Monster 검색
/// </summary>
public sealed class QueryStatement : Statement
{
    public QueryPattern Subject { get; }
    public string Relation { get; }           // IS, HAS, CAN
    public string? Target { get; }            // 타입명 또는 속성명
    public object? TargetValue { get; }       // HAS의 경우 값 (선택적)
    public Expression? WhereCondition { get; }

    public QueryStatement(
        QueryPattern subject,
        string relation,
        string? target,
        object? targetValue,
        Expression? whereCondition,
        int line,
        int column)
        : base(line, column)
    {
        Subject = subject;
        Relation = relation;
        Target = target;
        TargetValue = targetValue;
        WhereCondition = whereCondition;
    }

    public override string ToString()
    {
        var result = $"{Subject} {Relation}";
        if (Target is not null) result += $" {Target}";
        if (TargetValue is not null) result += $" {TargetValue}";
        if (WhereCondition is not null) result += $" WHERE {WhereCondition}";
        return result;
    }
}
