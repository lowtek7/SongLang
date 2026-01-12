using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 관계 문장 실행: Subject Relation [Object] [Value]
/// IS, HAS, PRINT 및 사용자 정의 관계 처리
/// </summary>
public sealed class RelationExecutor : IStatementExecutor<RelationStatement>
{
    public void Execute(RelationStatement stmt, ExecutionContext ctx)
    {
        var subject = ctx.ResolveNode(stmt.Subject);

        switch (stmt.Relation.ToUpperInvariant())
        {
            case "IS":
                ExecuteIs(subject, stmt, ctx);
                break;
            case "HAS":
                ExecuteHas(subject, stmt, ctx);
                break;
            case "CONTAINS":
                ExecuteContains(subject, stmt, ctx);
                break;
            case "IN":
                ExecuteIn(subject, stmt, ctx);
                break;
            case "PRINT":
                ExecutePrint(subject, ctx);
                break;
            default:
                ExecuteCustomRelation(subject, stmt, ctx);
                break;
        }
    }

    /// <summary>
    /// IS 관계 실행: Subject IS Object
    /// </summary>
    private static void ExecuteIs(Node subject, RelationStatement stmt, ExecutionContext ctx)
    {
        if (stmt.Object is null)
        {
            throw new InterpreterException("IS relation requires an object", stmt.Line, stmt.Column);
        }

        var parent = ctx.ResolveNode(stmt.Object);
        subject.AddParent(parent);
    }

    /// <summary>
    /// CONTAINS 관계 실행: Subject CONTAINS Object
    /// 예: Inventory CONTAINS Sword
    /// </summary>
    private static void ExecuteContains(Node subject, RelationStatement stmt, ExecutionContext ctx)
    {
        if (stmt.Object is null)
        {
            throw new InterpreterException("CONTAINS relation requires an object", stmt.Line, stmt.Column);
        }

        var child = ctx.ResolveNode(stmt.Object);
        subject.AddChild(child);
    }

    /// <summary>
    /// IN 관계 실행: Subject IN Container (역방향 CONTAINS)
    /// 예: Sword IN Inventory -> Inventory CONTAINS Sword
    /// </summary>
    private static void ExecuteIn(Node subject, RelationStatement stmt, ExecutionContext ctx)
    {
        if (stmt.Object is null)
        {
            throw new InterpreterException("IN relation requires a container", stmt.Line, stmt.Column);
        }

        var container = ctx.ResolveNode(stmt.Object);
        container.AddChild(subject);
    }

    /// <summary>
    /// HAS 관계 실행: Subject HAS Property Value
    /// 값이 존재하는 노드 이름이면 노드 참조로 저장
    /// </summary>
    private static void ExecuteHas(Node subject, RelationStatement stmt, ExecutionContext ctx)
    {
        if (stmt.Object is null)
        {
            throw new InterpreterException("HAS relation requires a property name", stmt.Line, stmt.Column);
        }

        var value = stmt.Value;

        // 값이 문자열이고 존재하는 노드 이름이면 노드 참조로 저장
        if (value is string nodeName)
        {
            var node = ctx.Graph.GetNode(nodeName);
            if (node is not null)
            {
                value = node;  // 노드 참조로 저장
            }
        }

        subject.SetProperty(stmt.Object, value);
    }

    /// <summary>
    /// PRINT 관계 실행: Subject PRINT
    /// Name 속성을 출력, 없으면 노드 이름 출력
    /// </summary>
    private static void ExecutePrint(Node subject, ExecutionContext ctx)
    {
        var name = subject.GetProperty("Name");

        if (name is null)
        {
            ctx.Output.WriteLine(subject.Name);
        }
        else if (name is Node nodeRef)
        {
            // Name이 노드 참조인 경우 (노드 이름이 기존 노드와 같을 때 발생)
            // 그 노드의 Name을 출력하거나, 없으면 노드 이름 출력
            var refName = nodeRef.GetProperty("Name");
            ctx.Output.WriteLine(refName is string s ? s : nodeRef.Name);
        }
        else
        {
            ctx.Output.WriteLine(name);
        }
    }

    /// <summary>
    /// 사용자 정의 관계 실행
    /// </summary>
    private static void ExecuteCustomRelation(Node subject, RelationStatement stmt, ExecutionContext ctx)
    {
        // 관계 노드 찾기
        var relationNode = ctx.Graph.GetNode(stmt.Relation);

        if (relationNode is null)
        {
            // 관계가 정의되지 않았으면 단순히 속성처럼 처리
            if (stmt.Object is not null)
            {
                var objNode = ctx.Graph.GetOrCreateNode(stmt.Object);
                subject.SetProperty($"_{stmt.Relation}", objNode.Name);
            }
            return;
        }

        // 관계가 RELATION인지 확인
        if (!relationNode.Is("RELATION"))
        {
            throw new InterpreterException($"'{stmt.Relation}' is not a relation", stmt.Line, stmt.Column);
        }

        // 대상 노드 가져오기 (컨텍스트에서 먼저 해석)
        Node? targetNode = null;
        if (stmt.Arguments.Count > 0)
        {
            var targetName = stmt.Arguments[0]?.ToString();
            if (targetName is not null)
            {
                targetNode = ctx.ResolveNode(targetName);
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
            // 반환값 초기화 (GIVES에서 설정됨)
            ctx.HasReturnValue = false;
            ctx.ReturnValue = null;

            if (roles is not null && roles.Count > 0)
            {
                // 역할 기반 바인딩: 첫 번째 역할 = subject, 나머지 = arguments
                ctx.Variables[roles[0]] = subject;

                for (int i = 1; i < roles.Count; i++)
                {
                    var argName = stmt.Arguments[i - 1]?.ToString();
                    if (argName is not null)
                    {
                        // 컨텍스트에서 먼저 해석 (중첩 관계 호출 지원)
                        ctx.Variables[roles[i]] = ctx.ResolveNode(argName);
                    }
                }

                ctx.Execute(doBody);

                // 컨텍스트 정리
                foreach (var role in roles)
                {
                    ctx.Variables.Remove(role);
                }
            }
            else
            {
                // 역할이 정의되지 않은 경우 그냥 실행
                ctx.Execute(doBody);
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
}
