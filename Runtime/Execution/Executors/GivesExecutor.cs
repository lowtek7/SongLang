using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// GIVES 문장 실행: GIVES Expression
/// DO 블록 내에서 값을 반환한다.
/// </summary>
public sealed class GivesExecutor : IStatementExecutor<GivesStatement>
{
    public void Execute(GivesStatement stmt, ExecutionContext ctx)
    {
        ctx.ReturnValue = ctx.EvaluateExpression(stmt.Value);
        ctx.HasReturnValue = true;
    }
}
