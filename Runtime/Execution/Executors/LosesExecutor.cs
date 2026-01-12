using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// LOSES 실행: Subject LOSES [IS] Target
/// </summary>
public sealed class LosesExecutor : IStatementExecutor<LosesStatement>
{
    public void Execute(LosesStatement stmt, ExecutionContext ctx)
    {
        var subject = ctx.ResolveNode(stmt.Subject);

        switch (stmt.Type)
        {
            case LosesType.Is:
                // IS 관계 제거 (부모에서 제거)
                // Target도 컨텍스트에서 해석 시도
                var parent = ctx.ResolveNodeOrNull(stmt.Target);
                if (parent is not null)
                {
                    subject.RemoveParent(parent);
                }
                break;

            case LosesType.Contains:
                // CONTAINS 관계 제거 (자식에서 제거)
                var child = ctx.ResolveNodeOrNull(stmt.Target);
                if (child is not null)
                {
                    subject.RemoveChild(child);
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
}
