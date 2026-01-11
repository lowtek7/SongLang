using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// CAN 문장 실행: Subject CAN Ability
/// </summary>
public sealed class CanExecutor : IStatementExecutor<CanStatement>
{
    public void Execute(CanStatement stmt, ExecutionContext ctx)
    {
        var subject = ctx.ResolveNode(stmt.Subject);

        // 능력을 HashSet으로 저장
        var abilities = subject.GetInternalProperty("Abilities") as HashSet<string>;
        if (abilities is null)
        {
            abilities = [];
            subject.SetInternalProperty("Abilities", abilities);
        }

        abilities.Add(stmt.Ability);
    }
}
