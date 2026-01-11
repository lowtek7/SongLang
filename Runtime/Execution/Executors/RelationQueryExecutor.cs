using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 관계 쿼리 실행: Subject RELATION ? 또는 ? RELATION Object
/// </summary>
public sealed class RelationQueryExecutor : IStatementExecutor<RelationQueryStatement>
{
    public void Execute(RelationQueryStatement stmt, ExecutionContext ctx)
    {
        var results = new List<(Node Source, string Relation, Node Target)>();

        bool isForwardQuery = stmt.Subject is not null && stmt.Object is null;
        bool isReverseQuery = stmt.Subject is null && stmt.Object is not null;

        if (isForwardQuery)
        {
            ExecuteForwardQuery(stmt, ctx, results);
        }
        else if (isReverseQuery)
        {
            ExecuteReverseQuery(stmt, ctx, results);
        }
        else
        {
            ExecuteAllQuery(stmt, ctx, results);
        }

        // 결과 출력
        if (results.Count == 0)
        {
            ctx.Output.WriteLine("(no relations)");
        }
        else
        {
            foreach (var (source, rel, target) in results)
            {
                ctx.Output.WriteLine($"{source.Name} {rel} {target.Name}");
            }
        }
    }

    /// <summary>
    /// 패턴: Player OWNS ? - subject의 모든 관계 대상 찾기
    /// </summary>
    private static void ExecuteForwardQuery(
        RelationQueryStatement stmt,
        ExecutionContext ctx,
        List<(Node, string, Node)> results)
    {
        var subjectNode = ctx.ResolveNode(stmt.Subject!);
        var relationName = stmt.RelationName.Equals("HAS", StringComparison.OrdinalIgnoreCase)
            ? null
            : stmt.RelationName;

        foreach (var instance in subjectNode.GetRelationInstances(relationName))
        {
            // 양방향 관계의 경우 역방향 인스턴스도 포함
            if (!instance.IsInverse)
            {
                results.Add((subjectNode, instance.RelationName, instance.Target));
            }
            else if (ctx.IsBidirectionalRelation(instance.RelationName))
            {
                // 양방향 관계: 역방향도 정방향처럼 출력
                results.Add((subjectNode, instance.RelationName, instance.Target));
            }
        }
    }

    /// <summary>
    /// 패턴: ? OWNS Sword - 특정 대상을 가진 모든 source 찾기
    /// </summary>
    private static void ExecuteReverseQuery(
        RelationQueryStatement stmt,
        ExecutionContext ctx,
        List<(Node, string, Node)> results)
    {
        var targetNode = ctx.ResolveNode(stmt.Object!);
        var relationName = stmt.RelationName.Equals("HAS", StringComparison.OrdinalIgnoreCase)
            ? null
            : stmt.RelationName;

        foreach (var node in ctx.Graph.AllNodes)
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

    /// <summary>
    /// 패턴: ? OWNS ? - 모든 관계 찾기
    /// </summary>
    private static void ExecuteAllQuery(
        RelationQueryStatement stmt,
        ExecutionContext ctx,
        List<(Node, string, Node)> results)
    {
        var relationName = stmt.RelationName.Equals("HAS", StringComparison.OrdinalIgnoreCase)
            ? null
            : stmt.RelationName;

        foreach (var node in ctx.Graph.AllNodes)
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
}
