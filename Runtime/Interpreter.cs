using Song.Parser;

namespace Song.Runtime;

/// <summary>
/// Song 언어의 인터프리터
/// Statement 리스트를 실행한다.
/// </summary>
public sealed class Interpreter
{
    private readonly Graph _graph = new();
    private readonly TextWriter _output;

    // 현재 실행 컨텍스트 (DO 블록 내에서 사용)
    private readonly Dictionary<string, object?> _context = [];

    // WHEN 표현식 컨텍스트 (조건식 평가 시 bare 식별자를 Subject의 속성으로 해석)
    private Node? _whenSubject;

    // 랜덤 생성기 (RANDOM, CHANCE용)
    private static readonly Random _random = new();

    public Graph Graph => _graph;

    public Interpreter() : this(Console.Out)
    {
    }

    public Interpreter(TextWriter output)
    {
        _output = output;
    }

    /// <summary>
    /// 문장 리스트 실행
    /// </summary>
    public void Execute(List<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            ExecuteStatement(stmt);
        }
    }

    private void ExecuteStatement(Statement stmt)
    {
        switch (stmt)
        {
            case RelationStatement rel:
                ExecuteRelation(rel);
                break;
            case HasExpressionStatement hasExpr:
                ExecuteHasExpression(hasExpr);
                break;
            case RoleDefinitionStatement roleDef:
                ExecuteRoleDefinition(roleDef);
                break;
            case DoBlockStatement doBlock:
                ExecuteDoBlock(doBlock);
                break;
            case CanStatement can:
                ExecuteCan(can);
                break;
            case LosesStatement loses:
                ExecuteLoses(loses);
                break;
            case DebugStatement debug:
                ExecuteDebug(debug);
                break;
            case WhenStatement whenStmt:
                ExecuteWhen(whenStmt);
                break;
            case WhenExpressionStatement whenExpr:
                ExecuteWhenExpression(whenExpr);
                break;
            case AllStatement allStmt:
                ExecuteAll(allStmt);
                break;
            case EachStatement eachStmt:
                ExecuteEach(eachStmt);
                break;
            case QueryStatement queryStmt:
                ExecuteQuery(queryStmt);
                break;
            case ExpressionPrintStatement exprPrint:
                ExecuteExpressionPrint(exprPrint);
                break;
            case ExpressionHasStatement exprHas:
                ExecuteExpressionHas(exprHas);
                break;
            case ChanceStatement chance:
                ExecuteChance(chance);
                break;
            case MetaPropertyStatement metaProp:
                ExecuteMetaProperty(metaProp);
                break;
            case RelationQueryStatement relQuery:
                ExecuteRelationQuery(relQuery);
                break;
            default:
                throw new InterpreterException($"Unknown statement type: {stmt.GetType().Name}", stmt.Line, stmt.Column);
        }
    }

    /// <summary>
    /// DEBUG 명령 실행
    /// </summary>
    private void ExecuteDebug(DebugStatement stmt)
    {
        switch (stmt.Target)
        {
            case DebugTarget.Graph:
                DumpGraph();
                break;
            case DebugTarget.Tokens:
            case DebugTarget.Ast:
                _output.WriteLine($"DEBUG {stmt.Target} is not yet implemented.");
                break;
        }
    }

    /// <summary>
    /// 그래프 상태 덤프
    /// </summary>
    public void DumpGraph()
    {
        _output.WriteLine("--- Graph State ---");
        if (_graph.Count == 0)
        {
            _output.WriteLine("(empty)");
            return;
        }

        foreach (var node in _graph.AllNodes)
        {
            _output.WriteLine(FormatNode(node));
        }
        _output.WriteLine("-------------------");
    }

    private static string FormatNode(Node node)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"Node({node.Name})");

        if (node.Parents.Count > 0)
        {
            sb.Append($" IS {string.Join(", ", node.Parents.Select(p => p.Name))}");
        }

        // 내부 속성(_로 시작)을 제외한 속성들 출력
        var visibleProps = node.Properties
            .Where(p => !p.Key.StartsWith('_'))
            .ToList();

        if (visibleProps.Count > 0)
        {
            var props = visibleProps.Select(p =>
            {
                var val = p.Value switch
                {
                    string s => $"\"{s}\"",
                    Node n => $"→{n.Name}",  // 노드 참조 표시
                    _ => p.Value
                };
                return $"{p.Key}={val}";
            });
            sb.Append($" {{ {string.Join(", ", props)} }}");
        }

        // 쿼리 결과 노드인 경우 Items 출력
        var items = node.InternalProperties.TryGetValue("Items", out var it)
            ? it as List<Node>
            : null;
        if (items is { Count: > 0 })
        {
            sb.Append($" CONTAINS [{string.Join(", ", items.Select(n => n.Name))}]");
        }

        // 능력 출력
        var abilities = node.InternalProperties.TryGetValue("Abilities", out var ab)
            ? ab as HashSet<string>
            : null;
        if (abilities is { Count: > 0 })
        {
            sb.Append($" CAN [{string.Join(", ", abilities)}]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 노드 이름을 해석 (컨텍스트 바인딩 우선, 없으면 그래프에서 가져오기/생성)
    /// </summary>
    private Node ResolveNode(string name)
    {
        // 컨텍스트에서 먼저 찾기 (EACH 등에서 바인딩된 변수)
        if (_context.TryGetValue(name, out var contextValue) && contextValue is Node node)
        {
            return node;
        }

        // 그래프에서 가져오거나 생성
        return _graph.GetOrCreateNode(name);
    }

    private void ExecuteRelation(RelationStatement stmt)
    {
        var subject = ResolveNode(stmt.Subject);

        switch (stmt.Relation.ToUpperInvariant())
        {
            case "IS":
                ExecuteIs(subject, stmt);
                break;
            case "HAS":
                ExecuteHas(subject, stmt);
                break;
            case "PRINT":
                ExecutePrint(subject, stmt);
                break;
            default:
                ExecuteCustomRelation(subject, stmt);
                break;
        }
    }

    /// <summary>
    /// IS 관계 실행: Subject IS Object
    /// </summary>
    private void ExecuteIs(Node subject, RelationStatement stmt)
    {
        if (stmt.Object is null)
        {
            throw new InterpreterException("IS relation requires an object", stmt.Line, stmt.Column);
        }

        var parent = ResolveNode(stmt.Object);
        subject.AddParent(parent);
    }

    /// <summary>
    /// HAS 관계 실행: Subject HAS Property Value
    /// 값이 존재하는 노드 이름이면 노드 참조로 저장
    /// </summary>
    private void ExecuteHas(Node subject, RelationStatement stmt)
    {
        if (stmt.Object is null)
        {
            throw new InterpreterException("HAS relation requires a property name", stmt.Line, stmt.Column);
        }

        var value = stmt.Value;

        // 값이 문자열이고 존재하는 노드 이름이면 노드 참조로 저장
        if (value is string nodeName)
        {
            var node = _graph.GetNode(nodeName);
            if (node is not null)
            {
                value = node;  // 노드 참조로 저장
            }
        }

        subject.SetProperty(stmt.Object, value);
    }

    /// <summary>
    /// 표현식 기반 HAS 실행: Subject HAS Property (Expression)
    /// </summary>
    private void ExecuteHasExpression(HasExpressionStatement stmt)
    {
        var subject = ResolveNode(stmt.Subject);
        var value = EvaluateExpression(stmt.ValueExpression);
        subject.SetProperty(stmt.Property, value);
    }

    /// <summary>
    /// PRINT 관계 실행: Subject PRINT
    /// Name 속성을 출력, 없으면 노드 이름 출력
    /// </summary>
    private void ExecutePrint(Node subject, RelationStatement stmt)
    {
        var name = subject.GetProperty("Name");

        if (name is null)
        {
            _output.WriteLine(subject.Name);
        }
        else
        {
            _output.WriteLine(name);
        }
    }

    /// <summary>
    /// 표현식 PRINT 실행: Expression PRINT
    /// 예: Player.HP.Current PRINT
    /// </summary>
    private void ExecuteExpressionPrint(ExpressionPrintStatement stmt)
    {
        var value = EvaluateExpression(stmt.Subject);
        if (value is Node node)
        {
            // 노드인 경우 Name 속성 또는 노드 이름 출력
            var name = node.GetProperty("Name");
            _output.WriteLine(name ?? node.Name);
        }
        else
        {
            _output.WriteLine(value?.ToString() ?? "null");
        }
    }

    /// <summary>
    /// 표현식 HAS 실행: Expression HAS Property Value
    /// 예: Player.HP HAS Current 100
    /// </summary>
    private void ExecuteExpressionHas(ExpressionHasStatement stmt)
    {
        // 주어 표현식을 평가하여 노드를 얻음
        var subjectValue = EvaluateExpression(stmt.Subject);
        if (subjectValue is not Node subjectNode)
        {
            throw new InterpreterException($"HAS subject must be a node", stmt.Line, stmt.Column);
        }

        // 값 결정
        object? value;
        if (stmt.ValueExpression is not null)
        {
            value = EvaluateExpression(stmt.ValueExpression);
        }
        else
        {
            value = stmt.Value;
            // 값이 문자열이고 존재하는 노드 이름이면 노드 참조로 저장
            if (value is string nodeName)
            {
                var node = _graph.GetNode(nodeName);
                if (node is not null)
                {
                    value = node;
                }
            }
        }

        subjectNode.SetProperty(stmt.Property, value);
    }

    /// <summary>
    /// 역할 정의 실행: Subject HAS RoleName (Node)
    /// </summary>
    private void ExecuteRoleDefinition(RoleDefinitionStatement stmt)
    {
        var subject = _graph.GetOrCreateNode(stmt.Subject);

        // 역할 목록 가져오거나 생성
        var roles = subject.GetInternalProperty("Roles") as List<string>;
        if (roles is null)
        {
            roles = [];
            subject.SetInternalProperty("Roles", roles);
        }

        // 역할 추가 (중복 방지)
        if (!roles.Contains(stmt.RoleName))
        {
            roles.Add(stmt.RoleName);
        }
    }

    /// <summary>
    /// 사용자 정의 관계 실행
    /// </summary>
    private void ExecuteCustomRelation(Node subject, RelationStatement stmt)
    {
        // 관계 노드 찾기
        var relationNode = _graph.GetNode(stmt.Relation);

        if (relationNode is null)
        {
            // 관계가 정의되지 않았으면 단순히 속성처럼 처리
            if (stmt.Object is not null)
            {
                var objNode = _graph.GetOrCreateNode(stmt.Object);
                subject.SetProperty($"_{stmt.Relation}", objNode.Name);
            }
            return;
        }

        // 관계가 RELATION인지 확인
        if (!relationNode.Is("RELATION"))
        {
            throw new InterpreterException($"'{stmt.Relation}' is not a relation", stmt.Line, stmt.Column);
        }

        // 대상 노드 가져오기
        Node? targetNode = null;
        if (stmt.Arguments.Count > 0)
        {
            var targetName = stmt.Arguments[0]?.ToString();
            if (targetName is not null)
            {
                targetNode = _graph.GetOrCreateNode(targetName);
            }
        }

        // 역할 정의 가져오기
        var roles = relationNode.GetInternalProperty("Roles") as List<string>;

        // 역할-인자 개수 검증
        if (roles is not null && roles.Count > 0)
        {
            var expectedArgCount = roles.Count - 1;  // 첫 번째 역할은 subject
            var actualArgCount = stmt.Arguments.Count;

            if (actualArgCount < expectedArgCount)
            {
                var missingRoles = roles.Skip(actualArgCount + 1).ToList();
                throw new InterpreterException(
                    $"Relation '{stmt.Relation}' expects {expectedArgCount} argument(s) but got {actualArgCount}. " +
                    $"Missing role(s): {string.Join(", ", missingRoles)}. " +
                    $"Usage: {subject.Name} {stmt.Relation} {string.Join(" ", roles.Skip(1))}",
                    stmt.Line, stmt.Column);
            }
            else if (actualArgCount > expectedArgCount)
            {
                throw new InterpreterException(
                    $"Relation '{stmt.Relation}' expects {expectedArgCount} argument(s) but got {actualArgCount}. " +
                    $"Defined roles: {roles[0]} (subject), {string.Join(", ", roles.Skip(1))} (arguments)",
                    stmt.Line, stmt.Column);
            }
        }

        // DO 블록 실행
        var doBody = relationNode.GetInternalProperty("DoBody") as List<Statement>;
        if (doBody is not null)
        {
            if (roles is not null && roles.Count > 0)
            {
                // 역할 기반 바인딩: 첫 번째 역할 = subject, 나머지 = arguments
                _context[roles[0]] = subject;

                for (int i = 1; i < roles.Count; i++)
                {
                    var argName = stmt.Arguments[i - 1]?.ToString();
                    if (argName is not null)
                    {
                        _context[roles[i]] = _graph.GetOrCreateNode(argName);
                    }
                }

                Execute(doBody);

                // 컨텍스트 정리
                foreach (var role in roles)
                {
                    _context.Remove(role);
                }
            }
            else
            {
                // 역할이 정의되지 않은 경우 그냥 실행
                Execute(doBody);
            }
        }

        // 관계 인스턴스 추적
        if (targetNode is not null)
        {
            // 순방향 관계 인스턴스 추가
            subject.AddRelationInstance(new RelationInstance(stmt.Relation, targetNode));

            // 역관계 처리
            var inverseName = relationNode.GetInternalProperty("Inverse") as string;
            if (inverseName is not null)
            {
                targetNode.AddRelationInstance(new RelationInstance(
                    inverseName, subject, isInverse: true, originalRelation: stmt.Relation));
            }

            // 양방향 처리
            var isBidirectional = relationNode.GetInternalProperty("Bidirectional") as bool? ?? false;
            if (isBidirectional)
            {
                targetNode.AddRelationInstance(new RelationInstance(
                    stmt.Relation, subject, isInverse: true, originalRelation: stmt.Relation));
            }
        }
    }

    /// <summary>
    /// DO 블록 정의 (즉시 실행하지 않고 저장)
    /// </summary>
    private void ExecuteDoBlock(DoBlockStatement stmt)
    {
        var subject = ResolveNode(stmt.Subject);
        subject.SetInternalProperty("DoBody", stmt.Body);
    }

    /// <summary>
    /// 메타 속성 실행: Subject HAS INVERSE/DIRECTION Value
    /// </summary>
    private void ExecuteMetaProperty(MetaPropertyStatement stmt)
    {
        var relationNode = _graph.GetOrCreateNode(stmt.Subject);

        // 관계가 RELATION인지 확인
        if (!relationNode.Is("RELATION"))
        {
            throw new InterpreterException(
                $"'{stmt.Subject}' is not a relation. IS RELATION must be declared first.",
                stmt.Line, stmt.Column);
        }

        switch (stmt.Type)
        {
            case MetaPropertyType.Inverse:
                // 역관계 이름 저장
                relationNode.SetInternalProperty("Inverse", stmt.Value);

                // 역관계 노드 자동 생성
                var inverseNode = _graph.GetOrCreateNode(stmt.Value);
                if (!inverseNode.Is("RELATION"))
                {
                    var relationParent = _graph.GetOrCreateNode("RELATION");
                    inverseNode.AddParent(relationParent);
                }

                // 역방향 참조 저장
                inverseNode.SetInternalProperty("InverseOf", stmt.Subject);

                // 역할 복사 (순서 교환)
                var roles = relationNode.GetInternalProperty("Roles") as List<string>;
                if (roles is not null && roles.Count >= 2)
                {
                    var inverseRoles = new List<string> { roles[1], roles[0] };
                    if (roles.Count > 2)
                    {
                        inverseRoles.AddRange(roles.Skip(2));
                    }
                    inverseNode.SetInternalProperty("Roles", inverseRoles);
                }
                break;

            case MetaPropertyType.Direction:
                var dirValue = stmt.Value.ToUpperInvariant();
                relationNode.SetInternalProperty("Direction", dirValue);

                if (dirValue == "BIDIRECTIONAL")
                {
                    relationNode.SetInternalProperty("Bidirectional", true);
                }
                break;
        }
    }

    /// <summary>
    /// 관계 쿼리 실행: Subject RELATION ? 또는 ? RELATION Object
    /// </summary>
    private void ExecuteRelationQuery(RelationQueryStatement stmt)
    {
        var results = new List<(Node Source, string Relation, Node Target)>();

        bool isForwardQuery = stmt.Subject is not null && stmt.Object is null;
        bool isReverseQuery = stmt.Subject is null && stmt.Object is not null;

        if (isForwardQuery)
        {
            // 패턴: Player OWNS ? - subject의 모든 관계 대상 찾기
            var subjectNode = ResolveNode(stmt.Subject!);
            var relationName = stmt.RelationName.Equals("HAS", StringComparison.OrdinalIgnoreCase) ? null : stmt.RelationName;

            foreach (var instance in subjectNode.GetRelationInstances(relationName))
            {
                // 양방향 관계의 경우 역방향 인스턴스도 포함
                if (!instance.IsInverse)
                {
                    results.Add((subjectNode, instance.RelationName, instance.Target));
                }
                else if (IsBidirectionalRelation(instance.RelationName))
                {
                    // 양방향 관계: 역방향도 정방향처럼 출력
                    results.Add((subjectNode, instance.RelationName, instance.Target));
                }
            }
        }
        else if (isReverseQuery)
        {
            // 패턴: ? OWNS Sword - 특정 대상을 가진 모든 source 찾기
            var targetNode = ResolveNode(stmt.Object!);
            var relationName = stmt.RelationName.Equals("HAS", StringComparison.OrdinalIgnoreCase) ? null : stmt.RelationName;

            foreach (var node in _graph.AllNodes)
            {
                foreach (var instance in node.GetRelationInstances(relationName))
                {
                    if (instance.Target == targetNode && !instance.IsInverse)
                    {
                        results.Add((node, instance.RelationName, targetNode));
                    }
                }
            }
        }
        else
        {
            // 패턴: ? OWNS ? - 모든 관계 찾기
            var relationName = stmt.RelationName.Equals("HAS", StringComparison.OrdinalIgnoreCase) ? null : stmt.RelationName;

            foreach (var node in _graph.AllNodes)
            {
                foreach (var instance in node.GetRelationInstances(relationName))
                {
                    if (!instance.IsInverse)
                    {
                        results.Add((node, instance.RelationName, instance.Target));
                    }
                }
            }
        }

        // 결과 출력
        if (results.Count == 0)
        {
            _output.WriteLine("(no relations)");
        }
        else
        {
            foreach (var (source, rel, target) in results)
            {
                _output.WriteLine($"{source.Name} {rel} {target.Name}");
            }
        }
    }

    /// <summary>
    /// 관계가 양방향인지 확인
    /// </summary>
    private bool IsBidirectionalRelation(string relationName)
    {
        var relationNode = _graph.GetNode(relationName);
        if (relationNode is null) return false;
        return relationNode.GetInternalProperty("Bidirectional") as bool? ?? false;
    }

    /// <summary>
    /// CAN 실행: Subject CAN Ability
    /// </summary>
    private void ExecuteCan(CanStatement stmt)
    {
        var subject = ResolveNode(stmt.Subject);

        // 능력을 HashSet으로 저장
        var abilities = subject.GetInternalProperty("Abilities") as HashSet<string>;
        if (abilities is null)
        {
            abilities = [];
            subject.SetInternalProperty("Abilities", abilities);
        }

        abilities.Add(stmt.Ability);
    }

    /// <summary>
    /// LOSES 실행: Subject LOSES [IS] Target
    /// </summary>
    private void ExecuteLoses(LosesStatement stmt)
    {
        var subject = ResolveNode(stmt.Subject);

        switch (stmt.Type)
        {
            case LosesType.Is:
                // IS 관계 제거 (부모에서 제거)
                // Target도 컨텍스트에서 해석 시도
                var parent = ResolveNodeOrNull(stmt.Target);
                if (parent is not null)
                {
                    subject.RemoveParent(parent);
                }
                break;

            case LosesType.Auto:
                // 자동 감지: 능력 먼저, 그 다음 속성
                var abilities = subject.GetInternalProperty("Abilities") as HashSet<string>;
                if (abilities?.Contains(stmt.Target) == true)
                {
                    abilities.Remove(stmt.Target);
                }
                else if (subject.HasOwnProperty(stmt.Target))
                {
                    subject.RemoveProperty(stmt.Target);
                }
                break;
        }
    }

    /// <summary>
    /// 노드 이름을 해석 (없으면 null 반환)
    /// </summary>
    private Node? ResolveNodeOrNull(string name)
    {
        // 컨텍스트에서 먼저 찾기
        if (_context.TryGetValue(name, out var contextValue) && contextValue is Node node)
        {
            return node;
        }

        // 그래프에서 찾기 (생성하지 않음)
        return _graph.GetNode(name);
    }

    /// <summary>
    /// WHEN 실행: Statement WHEN DO ... END
    /// 조건이 참이면 블록 실행
    /// </summary>
    private void ExecuteWhen(WhenStatement stmt)
    {
        if (EvaluateCondition(stmt.Condition))
        {
            Execute(stmt.Body);
        }
    }

    /// <summary>
    /// WHEN 표현식 실행: Subject WHEN (condition) DO ... [ELSE ...] END
    /// 표현식이 참이면 본문 실행, 거짓이면 ELSE 실행
    /// </summary>
    private void ExecuteWhenExpression(WhenExpressionStatement stmt)
    {
        // Subject를 컨텍스트에 바인딩 (조건식에서 Subject.Property 접근 가능)
        var subjectNode = _graph.GetNode(stmt.Subject);

        if (subjectNode is not null)
        {
            _context[stmt.Subject] = subjectNode;
        }

        // WHEN subject 설정 (bare 식별자를 속성으로 해석)
        var previousWhenSubject = _whenSubject;
        _whenSubject = subjectNode;

        try
        {
            // 조건 평가
            var result = EvaluateExpression(stmt.Condition);

            if (IsTruthy(result))
            {
                Execute(stmt.Body);
            }
            else
            {
                // ELSE WHEN 체이닝
                if (stmt.ElseWhen is not null)
                {
                    ExecuteWhenExpression(stmt.ElseWhen);
                }
                // ELSE 블록
                else if (stmt.ElseBody is not null)
                {
                    Execute(stmt.ElseBody);
                }
            }
        }
        finally
        {
            _whenSubject = previousWhenSubject;
            if (subjectNode is not null)
            {
                _context.Remove(stmt.Subject);
            }
        }
    }

    /// <summary>
    /// CHANCE 실행: CHANCE percent DO ... [ELSE DO ...] END
    /// 확률에 따라 본문 또는 ELSE 실행
    /// </summary>
    private void ExecuteChance(ChanceStatement stmt)
    {
        // 확률 평가 (0~100)
        var percentValue = EvaluateExpression(stmt.Percent);
        var percent = ToNumber(percentValue, stmt.Percent);

        // 0~100 사이 랜덤 값 생성
        var roll = _random.Next(0, 100);

        if (roll < percent)
        {
            Execute(stmt.Body);
        }
        else if (stmt.ElseBody is not null)
        {
            Execute(stmt.ElseBody);
        }
    }

    /// <summary>
    /// 조건 문장 평가
    /// HAS 조건: 노드가 해당 속성을 가지고 있고 값이 일치하면 참
    /// IS 조건: 노드가 해당 타입인지 확인
    /// </summary>
    private bool EvaluateCondition(RelationStatement condition)
    {
        var node = _graph.GetNode(condition.Subject);
        if (node is null) return false;

        return condition.Relation.ToUpperInvariant() switch
        {
            "HAS" => EvaluateHasCondition(node, condition),
            "IS" => condition.Object is not null && node.Is(condition.Object),
            "CAN" => condition.Object is not null && node.Can(condition.Object),
            _ => false
        };
    }

    private bool EvaluateHasCondition(Node node, RelationStatement rel)
    {
        if (rel.Object is null) return false;

        var propValue = node.GetProperty(rel.Object);

        // 값이 지정되지 않았으면 속성 존재 여부만 확인
        if (rel.Value is null)
        {
            return propValue is not null;
        }

        // 값 비교
        if (propValue is null) return false;

        // 숫자 비교
        if (rel.Value is double dVal && propValue is double dProp)
        {
            return Math.Abs(dVal - dProp) < 0.0001;
        }

        return rel.Value.Equals(propValue);
    }

    /// <summary>
    /// ALL 실행: ALL TypeName [Action] 또는 ALL ?queryVar [Action]
    /// 해당 타입/쿼리 결과의 모든 노드에 대해 액션 실행
    /// </summary>
    private void ExecuteAll(AllStatement stmt)
    {
        List<Node> matchingNodes;

        // 쿼리 변수 사용 시 저장된 결과에서 가져오기
        if (stmt.QueryVariable is not null)
        {
            matchingNodes = GetQueryResults(stmt.QueryVariable);
            if (matchingNodes.Count == 0)
            {
                _output.WriteLine($"ALL ?{stmt.QueryVariable}: No query results found (run query first)");
                return;
            }
        }
        else
        {
            // 타입 이름으로 노드 찾기
            matchingNodes = _graph.AllNodes
                .Where(n => n.Is(stmt.TypeName))
                .ToList();
        }

        if (stmt.Action is null)
        {
            // 액션이 없으면 매칭된 노드 수 출력 (디버그용)
            var target = stmt.QueryVariable is not null ? $"?{stmt.QueryVariable}" : stmt.TypeName;
            _output.WriteLine($"ALL {target}: {matchingNodes.Count} nodes found");
            return;
        }

        // 각 노드에 대해 액션 실행
        foreach (var node in matchingNodes)
        {
            ExecuteActionOnNode(node, stmt.Action);
        }
    }

    /// <summary>
    /// 특정 노드에 대해 액션 실행 (노드 이름을 대체)
    /// </summary>
    private void ExecuteActionOnNode(Node node, Statement action)
    {
        if (action is RelationStatement rel)
        {
            // Subject를 실제 노드로 대체
            var newRel = new RelationStatement(
                node.Name,
                rel.Relation,
                rel.Object,
                rel.Value,
                rel.Line,
                rel.Column
            );
            ExecuteRelation(newRel);
        }
    }

    /// <summary>
    /// EACH 실행: Collection EACH Variable DO ... END
    /// 컬렉션의 각 항목에 대해 블록 실행
    /// </summary>
    private void ExecuteEach(EachStatement stmt)
    {
        var collectionNode = _graph.GetNode(stmt.Collection);
        if (collectionNode is null)
        {
            throw new InterpreterException($"Collection '{stmt.Collection}' not found", stmt.Line, stmt.Column);
        }

        // 컬렉션 노드의 자식들 (이 노드를 IS로 상속하는 노드들)
        var children = _graph.AllNodes
            .Where(n => n.Parents.Contains(collectionNode))
            .ToList();

        foreach (var child in children)
        {
            // 컨텍스트에 변수 바인딩
            _context[stmt.Variable] = child;

            Execute(stmt.Body);

            _context.Remove(stmt.Variable);
        }
    }

    /// <summary>
    /// 쿼리 실행: ?var IS/HAS/CAN Target [WHERE condition]
    /// 조건에 맞는 노드 검색, 결과를 노드로 저장
    /// </summary>
    private void ExecuteQuery(QueryStatement stmt)
    {
        // 조건에 맞는 노드 찾기
        var matchingNodes = FindMatchingNodes(stmt);

        // WHERE 조건으로 필터링
        if (stmt.WhereCondition is not null)
        {
            matchingNodes = FilterByWhereCondition(matchingNodes, stmt);
        }

        // 변수 바인딩이 있으면 결과를 노드로 저장
        if (!stmt.Subject.IsWildcard && stmt.Subject.VariableName is not null)
        {
            var varName = stmt.Subject.VariableName;

            // 쿼리 결과 노드 생성
            var resultNode = _graph.GetOrCreateNode(varName);

            // QueryResult 타입 부여
            var queryResultType = _graph.GetOrCreateNode("QueryResult");
            if (!resultNode.Parents.Contains(queryResultType))
            {
                resultNode.AddParent(queryResultType);
            }

            // 결과 노드들을 Items로 저장
            resultNode.SetInternalProperty("Items", matchingNodes);

            // 결과 출력
            _output.WriteLine($"Query ?{varName}: {matchingNodes.Count} nodes found");
            foreach (var node in matchingNodes)
            {
                _output.WriteLine($"  - {node.Name}");
            }
        }
        else
        {
            // 와일드카드인 경우 결과만 출력
            _output.WriteLine($"Query: {matchingNodes.Count} nodes found");
            foreach (var node in matchingNodes)
            {
                _output.WriteLine($"  - {node.Name}");
            }
        }
    }

    /// <summary>
    /// 쿼리 조건에 맞는 노드 찾기
    /// </summary>
    private List<Node> FindMatchingNodes(QueryStatement stmt)
    {
        var result = new List<Node>();

        foreach (var node in _graph.AllNodes)
        {
            bool matches = stmt.Relation switch
            {
                "IS" => MatchesIsQuery(node, stmt),
                "HAS" => MatchesHasQuery(node, stmt),
                "CAN" => MatchesCanQuery(node, stmt),
                _ => false
            };

            if (matches)
            {
                result.Add(node);
            }
        }

        return result;
    }

    private bool MatchesIsQuery(Node node, QueryStatement stmt)
    {
        if (stmt.Target is null) return true;  // ?x IS -> 모든 노드
        return node.Is(stmt.Target);
    }

    private bool MatchesHasQuery(Node node, QueryStatement stmt)
    {
        if (stmt.Target is null) return node.Properties.Count > 0;  // ?x HAS -> 속성 있는 노드

        var propValue = node.GetProperty(stmt.Target);

        // 속성이 없으면 매칭 안됨
        if (propValue is null) return false;

        // 값 지정이 없으면 속성 존재만 확인
        if (stmt.TargetValue is null) return true;

        // 값 비교
        if (stmt.TargetValue is double dVal && propValue is double dProp)
        {
            return Math.Abs(dVal - dProp) < 0.0001;
        }

        return stmt.TargetValue.Equals(propValue);
    }

    private bool MatchesCanQuery(Node node, QueryStatement stmt)
    {
        if (stmt.Target is null)
        {
            // ?x CAN -> 능력이 있는 노드
            var abilities = node.GetInternalProperty("Abilities") as HashSet<string>;
            return abilities is { Count: > 0 };
        }
        return node.Can(stmt.Target);
    }

    /// <summary>
    /// WHERE 조건으로 노드 필터링
    /// </summary>
    private List<Node> FilterByWhereCondition(List<Node> nodes, QueryStatement stmt)
    {
        var result = new List<Node>();
        var varName = stmt.Subject.VariableName ?? "_";

        foreach (var node in nodes)
        {
            // 컨텍스트에 현재 노드 바인딩
            _context[varName] = node;

            try
            {
                var conditionResult = EvaluateExpression(stmt.WhereCondition!);

                // boolean true로 평가되면 매칭
                if (conditionResult is bool b && b)
                {
                    result.Add(node);
                }
                else if (conditionResult is double d && d != 0)
                {
                    result.Add(node);  // 0이 아닌 숫자는 truthy
                }
            }
            catch
            {
                // 평가 실패 시 해당 노드는 제외
            }
            finally
            {
                _context.Remove(varName);
            }
        }

        return result;
    }

    /// <summary>
    /// 쿼리 결과 가져오기 (노드의 Items 내부 속성에서)
    /// </summary>
    public List<Node> GetQueryResults(string variableName)
    {
        var resultNode = _graph.GetNode(variableName);
        if (resultNode is not null)
        {
            // QueryResult 노드인지 확인
            if (resultNode.Is("QueryResult"))
            {
                var items = resultNode.GetInternalProperty("Items") as List<Node>;
                if (items is not null)
                {
                    return items;
                }
            }
        }
        return [];
    }

    #region Expression Evaluation

    private object? EvaluateExpression(Expression expr)
    {
        return expr switch
        {
            NumberExpression num => num.Value,
            StringExpression str => str.Value,
            IdentifierExpression id => ResolveIdentifier(id),
            PropertyAccessExpression prop => ResolvePropertyAccess(prop),
            BinaryExpression bin => EvaluateBinary(bin),
            UnaryExpression unary => EvaluateUnary(unary),
            GroupingExpression group => EvaluateExpression(group.Inner),
            RandomExpression rand => EvaluateRandom(rand),
            _ => throw new InterpreterException($"Unknown expression type: {expr.GetType().Name}", expr.Line, expr.Column)
        };
    }

    /// <summary>
    /// RANDOM 표현식 평가: RANDOM min max
    /// min 이상 max 이하 랜덤 정수 반환
    /// </summary>
    private double EvaluateRandom(RandomExpression expr)
    {
        var minValue = EvaluateExpression(expr.Min);
        var maxValue = EvaluateExpression(expr.Max);

        var min = (int)ToNumber(minValue, expr.Min);
        var max = (int)ToNumber(maxValue, expr.Max);

        // max 포함 (inclusive)
        return _random.Next(min, max + 1);
    }

    private object? ResolveIdentifier(IdentifierExpression id)
    {
        // 컨텍스트에서 먼저 찾기
        if (_context.TryGetValue(id.Name, out var contextValue))
        {
            return contextValue;
        }

        // 그래프에서 노드 찾기
        var node = _graph.GetNode(id.Name);
        if (node is not null)
        {
            return node;
        }

        // WHEN 컨텍스트에서 Subject의 속성으로 해석 시도
        if (_whenSubject is not null)
        {
            var propValue = _whenSubject.GetProperty(id.Name);
            if (propValue is not null)
            {
                return propValue;
            }
        }

        throw new SongError(ErrorType.NodeNotFound, $"\"{id.Name}\"", id.Line, id.Column);
    }

    private object? ResolvePropertyAccess(PropertyAccessExpression prop)
    {
        var obj = EvaluateExpression(prop.Object);

        if (obj is Node node)
        {
            var value = node.GetProperty(prop.Property);
            if (value is null)
            {
                throw new SongError(ErrorType.PropertyNotFound,
                    $"\"{node.Name}\" has no \"{prop.Property}\"", prop.Line, prop.Column);
            }
            return value;
        }

        throw new SongError(ErrorType.TypeMismatch,
            $"'{prop.Object}' is not a Node", prop.Line, prop.Column);
    }

    private object? EvaluateBinary(BinaryExpression bin)
    {
        // AND, OR은 short-circuit 평가
        if (bin.Operator == BinaryOperator.And)
        {
            var left = EvaluateExpression(bin.Left);
            if (!IsTruthy(left)) return false;
            return IsTruthy(EvaluateExpression(bin.Right));
        }

        if (bin.Operator == BinaryOperator.Or)
        {
            var left = EvaluateExpression(bin.Left);
            if (IsTruthy(left)) return true;
            return IsTruthy(EvaluateExpression(bin.Right));
        }

        var leftVal = EvaluateExpression(bin.Left);
        var rightVal = EvaluateExpression(bin.Right);

        return bin.Operator switch
        {
            BinaryOperator.Add => Add(leftVal, rightVal, bin),
            BinaryOperator.Subtract => Subtract(leftVal, rightVal, bin),
            BinaryOperator.Multiply => Multiply(leftVal, rightVal, bin),
            BinaryOperator.Divide => Divide(leftVal, rightVal, bin),
            BinaryOperator.Modulo => Modulo(leftVal, rightVal, bin),
            BinaryOperator.Equal => Equals(leftVal, rightVal),
            BinaryOperator.NotEqual => !Equals(leftVal, rightVal),
            BinaryOperator.LessThan => Compare(leftVal, rightVal, bin) < 0,
            BinaryOperator.GreaterThan => Compare(leftVal, rightVal, bin) > 0,
            BinaryOperator.LessEqual => Compare(leftVal, rightVal, bin) <= 0,
            BinaryOperator.GreaterEqual => Compare(leftVal, rightVal, bin) >= 0,
            _ => throw new InterpreterException($"Unknown operator: {bin.Operator}", bin.Line, bin.Column)
        };
    }

    private object? EvaluateUnary(UnaryExpression unary)
    {
        var operand = EvaluateExpression(unary.Operand);

        return unary.Operator switch
        {
            UnaryOperator.Negate => Negate(operand, unary),
            UnaryOperator.Not => !IsTruthy(operand),
            _ => throw new InterpreterException($"Unknown operator: {unary.Operator}", unary.Line, unary.Column)
        };
    }

    /// <summary>
    /// 값의 진위 판정 (truthy/falsy)
    /// </summary>
    private static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            double d => d != 0,
            int i => i != 0,
            string s => !string.IsNullOrEmpty(s),
            Node _ => true,
            _ => true
        };
    }

    /// <summary>
    /// Converts a value to a number (double).
    /// Note: All numbers in SongLang are double. The int case is defensive code
    /// for potential future C# API extensions where int values might be passed.
    /// </summary>
    private static double ToNumber(object? value, Expression expr)
    {
        return value switch
        {
            double d => d,
            int i => i,  // Defensive: currently unreachable, all parsed numbers are double
            bool b => b ? 1 : 0,
            null => throw new SongError(ErrorType.TypeMismatch, "null cannot be converted to Number", expr.Line, expr.Column),
            _ => throw new SongError(ErrorType.TypeMismatch, $"'{value}' ({value.GetType().Name}) cannot be converted to Number", expr.Line, expr.Column)
        };
    }

    private static object Add(object? left, object? right, Expression expr)
    {
        // 문자열 연결
        if (left is string || right is string)
        {
            return $"{left}{right}";
        }

        return ToNumber(left, expr) + ToNumber(right, expr);
    }

    private static double Subtract(object? left, object? right, Expression expr)
    {
        return ToNumber(left, expr) - ToNumber(right, expr);
    }

    private static double Multiply(object? left, object? right, Expression expr)
    {
        return ToNumber(left, expr) * ToNumber(right, expr);
    }

    private static double Divide(object? left, object? right, Expression expr)
    {
        var divisor = ToNumber(right, expr);
        if (divisor == 0)
        {
            throw new SongError(ErrorType.DivisionByZero, "Cannot divide by zero", expr.Line, expr.Column);
        }
        return ToNumber(left, expr) / divisor;
    }

    private static double Modulo(object? left, object? right, Expression expr)
    {
        var divisor = ToNumber(right, expr);
        if (divisor == 0)
        {
            throw new SongError(ErrorType.DivisionByZero, "Cannot modulo by zero", expr.Line, expr.Column);
        }
        return ToNumber(left, expr) % divisor;
    }

    private static double Negate(object? operand, Expression expr)
    {
        return -ToNumber(operand, expr);
    }

    private static int Compare(object? left, object? right, Expression expr)
    {
        var l = ToNumber(left, expr);
        var r = ToNumber(right, expr);
        return l.CompareTo(r);
    }

    private static new bool Equals(object? left, object? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    #endregion
}

/// <summary>
/// 노드가 특정 능력을 가지고 있는지 확인하는 확장 메서드
/// </summary>
public static class NodeExtensions
{
    public static bool Can(this Node node, string ability)
    {
        return CanWithVisited(node, ability, []);
    }

    private static bool CanWithVisited(Node node, string ability, HashSet<Node> visited)
    {
        // 순환 참조 방어
        if (!visited.Add(node))
        {
            return false;
        }

        var abilities = node.GetInternalProperty("Abilities") as HashSet<string>;
        if (abilities?.Contains(ability) == true)
        {
            return true;
        }

        // 부모에서 능력 상속 확인
        foreach (var parent in node.Parents)
        {
            if (CanWithVisited(parent, ability, visited))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 인터프리터 오류
/// </summary>
public class InterpreterException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public InterpreterException(string message, int line, int column)
        : base($"[{line}:{column}] {message}")
    {
        Line = line;
        Column = column;
    }
}
