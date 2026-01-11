using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// WHEN 실행: Statement WHEN DO ... END
/// 조건이 참이면 블록 실행
/// </summary>
public sealed class WhenExecutor : IStatementExecutor<WhenStatement>
{
    public void Execute(WhenStatement stmt, ExecutionContext ctx)
    {
        if (ctx.EvaluateCondition(stmt.Condition))
        {
            ctx.Execute(stmt.Body);
        }
    }
}
