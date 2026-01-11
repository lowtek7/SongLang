using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// DO 블록 정의 (즉시 실행하지 않고 저장)
/// </summary>
public sealed class DoBlockExecutor : IStatementExecutor<DoBlockStatement>
{
    public void Execute(DoBlockStatement stmt, ExecutionContext ctx)
    {
        var subject = ctx.ResolveNode(stmt.Subject);
        subject.SetInternalProperty("DoBody", stmt.Body);
    }
}
