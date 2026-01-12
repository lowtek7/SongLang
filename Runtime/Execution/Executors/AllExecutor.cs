using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// ALL 실행: ALL TypeName [Action] 또는 ALL ?queryVar [Action]
/// 해당 타입/쿼리 결과의 모든 노드에 대해 액션 실행
/// </summary>
public sealed class AllExecutor : IStatementExecutor<AllStatement>
{
    public void Execute(AllStatement stmt, ExecutionContext ctx)
    {
        List<Node> matchingNodes;

        // 쿼리 변수 사용 시 저장된 결과에서 가져오기
        if (stmt.QueryVariable is not null)
        {
            matchingNodes = ctx.GetQueryResults(stmt.QueryVariable);
            if (matchingNodes.Count == 0)
            {
                ctx.Output.WriteLine($"ALL ?{stmt.QueryVariable}: No query results found (run query first)");
                return;
            }
        }
        else
        {
            // 타입 인덱스를 사용하여 노드 찾기 (프로토타입 체인 포함)
            matchingNodes = ctx.Graph.GetAllNodesByType(stmt.TypeName).ToList();
        }

        if (stmt.Action is null)
        {
            // 액션이 없으면 매칭된 노드 수 출력 (디버그용)
            var target = stmt.QueryVariable is not null ? $"?{stmt.QueryVariable}" : stmt.TypeName;
            ctx.Output.WriteLine($"ALL {target}: {matchingNodes.Count} nodes found");
            return;
        }

        // 각 노드에 대해 액션 실행
        foreach (var node in matchingNodes)
        {
            ExecuteActionOnNode(node, stmt.Action, ctx);
        }
    }

    /// <summary>
    /// 특정 노드에 대해 액션 실행 (노드 이름을 대체)
    /// </summary>
    private static void ExecuteActionOnNode(Node node, Statement action, ExecutionContext ctx)
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
            ctx.ExecuteStatement(newRel);
        }
    }
}
