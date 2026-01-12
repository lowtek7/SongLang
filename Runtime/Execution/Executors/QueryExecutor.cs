using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 쿼리 실행: ?var IS/HAS/CAN Target [WHERE condition]
/// 조건에 맞는 노드 검색, 결과를 노드로 저장
/// </summary>
public sealed class QueryExecutor : IStatementExecutor<QueryStatement>
{
    public void Execute(QueryStatement stmt, ExecutionContext ctx)
    {
        // 조건에 맞는 노드 찾기
        var matchingNodes = FindMatchingNodes(stmt, ctx);

        // WHERE 조건으로 필터링
        if (stmt.WhereCondition is not null)
        {
            matchingNodes = FilterByWhereCondition(matchingNodes, stmt, ctx);
        }

        // 변수 바인딩이 있으면 결과를 노드로 저장
        if (!stmt.Subject.IsWildcard && stmt.Subject.VariableName is not null)
        {
            var varName = stmt.Subject.VariableName;

            // 쿼리 결과 노드 생성
            var resultNode = ctx.Graph.GetOrCreateNode(varName);

            // QueryResult 타입 부여
            var queryResultType = ctx.Graph.GetOrCreateNode("QueryResult");
            if (!resultNode.Parents.Contains(queryResultType))
            {
                resultNode.AddParent(queryResultType);
            }

            // 기존 Children 클리어 (쿼리 재실행 시)
            resultNode.Children.Clear();

            // 결과 노드들을 CONTAINS 관계(Children)로 저장
            foreach (var node in matchingNodes)
            {
                resultNode.AddChild(node);
            }

            // 결과 출력
            ctx.Output.WriteLine($"Query ?{varName}: {matchingNodes.Count} nodes found");
            foreach (var node in matchingNodes)
            {
                ctx.Output.WriteLine($"  - {node.Name}");
            }
        }
        else
        {
            // 와일드카드인 경우 결과만 출력
            ctx.Output.WriteLine($"Query: {matchingNodes.Count} nodes found");
            foreach (var node in matchingNodes)
            {
                ctx.Output.WriteLine($"  - {node.Name}");
            }
        }
    }

    /// <summary>
    /// 쿼리 조건에 맞는 노드 찾기
    /// </summary>
    private static List<Node> FindMatchingNodes(QueryStatement stmt, ExecutionContext ctx)
    {
        // IS 쿼리는 타입 인덱스를 사용하여 최적화
        if (stmt.Relation == "IS" && stmt.Target is not null)
        {
            return ctx.Graph.GetAllNodesByType(stmt.Target).ToList();
        }

        // IN 쿼리는 컨테이너의 Children을 직접 반환
        if (stmt.Relation == "IN" && stmt.Target is not null)
        {
            var container = ctx.Graph.GetNode(stmt.Target);
            return container?.Children.ToList() ?? [];
        }

        // 나머지 쿼리는 AllNodes 순회
        var result = new List<Node>();

        foreach (var node in ctx.Graph.AllNodes)
        {
            bool matches = stmt.Relation switch
            {
                "IS" => MatchesIsQuery(node, stmt),
                "HAS" => MatchesHasQuery(node, stmt),
                "CAN" => MatchesCanQuery(node, stmt),
                "IN" => MatchesInQuery(node, stmt, ctx),
                "CONTAINS" => MatchesContainsQuery(node, stmt),
                _ => false
            };

            if (matches)
            {
                result.Add(node);
            }
        }

        return result;
    }

    private static bool MatchesIsQuery(Node node, QueryStatement stmt)
    {
        if (stmt.Target is null) return true;  // ?x IS -> 모든 노드
        return node.Is(stmt.Target);
    }

    private static bool MatchesHasQuery(Node node, QueryStatement stmt)
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

    private static bool MatchesCanQuery(Node node, QueryStatement stmt)
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
    /// IN 쿼리: ?x IN Container -> Container의 Children에 포함된 노드
    /// </summary>
    private static bool MatchesInQuery(Node node, QueryStatement stmt, ExecutionContext ctx)
    {
        if (stmt.Target is null) return false;  // Container 필수

        var container = ctx.Graph.GetNode(stmt.Target);
        if (container is null) return false;

        return container.Children.Contains(node);
    }

    /// <summary>
    /// CONTAINS 쿼리: ?x CONTAINS -> Children이 있는 노드
    /// </summary>
    private static bool MatchesContainsQuery(Node node, QueryStatement stmt)
    {
        // Target이 없으면: Children이 있는 모든 노드
        if (stmt.Target is null) return node.Children.Count > 0;

        // Target이 있으면: 특정 노드를 Children으로 가진 노드
        return node.Children.Any(c => c.Name == stmt.Target);
    }

    /// <summary>
    /// WHERE 조건으로 노드 필터링
    /// </summary>
    private static List<Node> FilterByWhereCondition(List<Node> nodes, QueryStatement stmt, ExecutionContext ctx)
    {
        var result = new List<Node>();
        var varName = stmt.Subject.VariableName ?? "_";

        foreach (var node in nodes)
        {
            // 컨텍스트에 현재 노드 바인딩
            ctx.Variables[varName] = node;

            try
            {
                var conditionResult = ctx.EvaluateExpression(stmt.WhereCondition!);

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
                ctx.Variables.Remove(varName);
            }
        }

        return result;
    }
}
