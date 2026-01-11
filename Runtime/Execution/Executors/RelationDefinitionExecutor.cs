using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 인라인 역할 정의 관계 실행: Subject IS RELATION (Role1, Role2, ...)
/// 예: Attack IS RELATION (Attacker, Victim)
/// </summary>
public sealed class RelationDefinitionExecutor : IStatementExecutor<RelationDefinitionStatement>
{
    public void Execute(RelationDefinitionStatement stmt, ExecutionContext ctx)
    {
        var subject = ctx.Graph.GetOrCreateNode(stmt.Subject);
        var relation = ctx.Graph.GetOrCreateNode("RELATION");

        // RELATION 타입 설정
        subject.AddParent(relation);

        // 역할 목록 설정 (기존 역할 대체)
        subject.SetInternalProperty("Roles", new List<string>(stmt.Roles));
    }
}
